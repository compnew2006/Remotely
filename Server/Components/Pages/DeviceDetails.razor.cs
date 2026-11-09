using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Remotely.Server.Components.ModalContents;
using Remotely.Server.Enums;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services;
using Remotely.Server.Services.Stores;
using Remotely.Shared;
using Remotely.Shared.Entities;
using Remotely.Shared.Utilities;
using Remotely.Shared.ViewModels;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Remotely.Server.Components.Pages;

public partial class DeviceDetails : AuthComponentBase, IDisposable
{
    private readonly ConcurrentQueue<string> _logLines = new();
    private readonly ConcurrentQueue<ScriptResult> _scriptResults = new();
    private readonly ConcurrentDictionary<string, double> _fileUploadProgressLookup = new();

    private string? _alertMessage;
    private Device? _device;
    private bool _userHasAccess;
    private string? _inputDeviceId;
    private bool _isLoading = true;
    private DeviceGroup[] _deviceGroups = Array.Empty<DeviceGroup>();

    private CancellationTokenSource? _previewCts;
    private string? _previewFileName;
    private bool _previewAutoEnabled = true;
    private int _previewRefreshSeconds = 15;
    private DateTimeOffset? _previewTimestamp;

    [Parameter]
    public string ActiveTab { get; set; } = string.Empty;

    [Parameter]
    public string DeviceId { get; set; } = string.Empty;

    [Inject]
    private ICircuitConnection CircuitConnection { get; set; } = null!;

    [Inject]
    private IDataService DataService { get; set; } = null!;


    [Inject]
    private IJsInterop JsInterop { get; set; } = null!;

    [Inject]
    private IModalService ModalService { get; set; } = null!;

    [Inject]
    private NavigationManager NavManager { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private IScreenshotService ScreenshotService { get; set; } = null!;

    [Inject]
    private IChatSessionStore ChatCache { get; set; } = null!;

    [Inject]
    private IAgentHubSessionCache ServiceSessionCache { get; set; } = null!;

    [Inject]
    private ILogger<DeviceDetails> Logger { get; set; } = null!;

    private DesktopCapabilities DeviceCapabilities => _device is null
        ? new DesktopCapabilities(
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            FeatureSupport.Unknown,
            "Device is loading.")
        : DesktopCapabilityMatrix.For(_device);

    private static bool IsFeatureAvailable(FeatureSupport support) =>
        support is FeatureSupport.Supported or FeatureSupport.Conditional;

    private static string GetFeatureStatusLabel(FeatureSupport support) =>
        DesktopCapabilities.GetStatusLabel(support);

    private static string GetAudioFeatureStatusLabel(DesktopCapabilities capabilities) =>
        (capabilities.AudioRecording, capabilities.LiveAudio) switch
        {
            (FeatureSupport.Supported, FeatureSupport.Supported) => "Supported",
            (FeatureSupport.Unsupported, FeatureSupport.Supported) => "Live audio only",
            (FeatureSupport.Unsupported, FeatureSupport.Unsupported) => "Not supported",
            (FeatureSupport.Unknown, FeatureSupport.Unknown) => "Unverified",
            _ => $"Recording: {GetFeatureStatusLabel(capabilities.AudioRecording)}; " +
                $"live audio: {GetFeatureStatusLabel(capabilities.LiveAudio)}"
        };

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        EnsureUserSet();

        if (!string.IsNullOrWhiteSpace(DeviceId))
        {
            var deviceResult = await DataService.GetDevice(DeviceId);
            if (deviceResult.IsSuccess)
            {
                _device = deviceResult.Value;
                _userHasAccess = DataService.DoesUserHaveAccessToDevice(_device.ID, User);
            }
            else
            {
                ToastService.ShowToast2(deviceResult.Reason, Enums.ToastType.Warning);
            }
        }

        _deviceGroups = DataService.GetDeviceGroups(UserName);
        await Register<ReceiveLogsMessage, string>(
            CircuitConnection.ConnectionId,
            HandleReceiveLogsMessage);

        _isLoading = false;

        if (_device is not null && _userHasAccess)
        {
            StartPreviewLoop();
        }
    }

    public new void Dispose()
    {
        StopPreviewLoop();
        base.Dispose();
    }

    /// <summary>
    /// The address of the newest screenshot (newest fetch result) of this
    /// device, or null when no screenshot is available.
    /// </summary>
    private string? PreviewImageUrl =>
        _device is null || _previewFileName is null
            ? null
            : $"api/screenshots/{_device.ID}/{_previewFileName}?t={_previewTimestamp?.ToUnixTimeMilliseconds() ?? DateTimeOffset.Now.ToUnixTimeMilliseconds()}";

    private async Task RefreshPreviewOnce()
    {
        if (_device is null)
        {
            return;
        }

        var result = await CircuitConnection.CaptureScreenshotNow(_device.ID);
        if (!result.IsSuccess)
        {
            // Offline, paused by pause windows, and so on. The tile shows
            // the last screenshot that arrived, if any.
            return;
        }

        var screenshots = ScreenshotService.GetScreenshots(_device.ID, 1);
        var latest = screenshots.FirstOrDefault();

        if (latest is not null)
        {
            _previewFileName = latest.FileName;
            _previewTimestamp = latest.TimeStamp;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshPreviewNow()
    {
        await RefreshPreviewOnce();
    }

    private void OnPreviewAutoChanged(ChangeEventArgs args)
    {
        if (args.Value is not bool enabled)
        {
            return;
        }

        _previewAutoEnabled = enabled;

        if (enabled)
        {
            StartPreviewLoop();
        }
        else
        {
            StopPreviewLoop();
        }
    }

    private void StartPreviewLoop()
    {
        StopPreviewLoop();

        if (!_previewAutoEnabled)
        {
            return;
        }

        _previewCts = new CancellationTokenSource();
        _ = PreviewLoop(_previewCts.Token);
    }

    private async Task PreviewLoop(CancellationToken cancellationToken)
    {
        // Loading the settings on every restart keeps the interval in sync
        // with the server configuration dialog.
        var settings = await DataService.GetSettings();

        if (!settings.EnableScreenshotPreviews)
        {
            _previewCts?.Dispose();
            _previewCts = null;
            return;
        }

        _previewRefreshSeconds = Math.Clamp(settings.ScreenshotPreviewRefreshSeconds, 5, 300);

        await RefreshPreviewOnce();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_previewRefreshSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshPreviewOnce();
            }
        }
        catch (OperationCanceledException)
        {
            // The page was closed or navigated away.
        }
    }

    private void StopPreviewLoop()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
    }

    private async Task HandleReceiveLogsMessage(object subscriber, ReceiveLogsMessage message)
    {
        _logLines.Enqueue(message.LogChunk);
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteLogs()
    {
        if (_device is null)
        {
            return;
        }

        var result = await JsInterop.Confirm("Are you sure you want to delete the remote logs?");
        if (result)
        {
            await CircuitConnection.DeleteRemoteLogs(_device.ID);
            ToastService.ShowToast("Delete command sent.");
        }
    }

    private void EditFormKeyDown()
    {
        _alertMessage = string.Empty;
    }

    private void EvaluateDeviceIdInputKeyDown(KeyboardEventArgs args)
    {
        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            NavManager.NavigateTo($"/device-details/{_inputDeviceId}");
        }
    }

    private void GetRemoteLogs()
    {
        if (_device is null)
        {
            return;
        }

        _logLines.Clear();

        if (_device.IsOnline)
        {
            CircuitConnection.GetRemoteLogs(_device.ID);
        }
    }

    private void GetScriptHistory()
    {
        if (_device is null)
        {
            return;
        }

        EnsureUserSet();

        _scriptResults.Clear();

        if (User.IsAdministrator)
        {
            var results = DataService
                .GetAllScriptResults(User.OrganizationID, _device.ID)
                .OrderByDescending(x => x.TimeStamp);

            foreach (var result in results)
            {
                _scriptResults.Enqueue(result);
            }
        }
        else
        {
            var results = DataService
                .GetAllCommandResultsForUser(User.OrganizationID, UserName, _device.ID)
                .OrderByDescending(x => x.TimeStamp);

            foreach (var result in results)
            {
                _scriptResults.Enqueue(result);
            }
        }
    }

    private string GetTrimmedText(string? source, int stringLength)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "(none)";
        }

        if (source.Length <= stringLength)
        {
            return source;
        }

        return source[0..25] + "...";
    }

    private string GetTrimmedText(string[]? source, int stringLength)
    {
        source ??= Array.Empty<string>();
        return GetTrimmedText(string.Join("", source), stringLength);
    }

    private Task HandleValidSubmit()
    {
        if (_device is null)
        {
            return Task.CompletedTask;
        }

        DataService.UpdateDevice(
            _device.ID,
            _device.Tags,
            _device.Alias,
            _device.DeviceGroupID,
            _device.Notes);

        _alertMessage = "Device details saved.";
        ToastService.ShowToast("Device details saved.");

        return Task.CompletedTask;
    }

    private void NavigateToDeviceId()
    {
        NavManager.NavigateTo($"/device-details/{_inputDeviceId}");
    }

    private void ShowAllDisks()
    {
        if (_device is null)
        {
            return;
        }

        var disksString = JsonSerializer.Serialize(_device.Drives, JsonSerializerHelper.IndentedOptions);
        void modalBody(RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0, $"<div style='white-space: pre'>{disksString}</div>");
        }
        ModalService.ShowModal($"All Disks for {_device.DeviceName}", modalBody);
    }

    private void ShowFullScriptOutput(ScriptResult result)
    {
        void outputModal(RenderTreeBuilder builder)
        {
            var output = string.Join("\r\n", result.StandardOutput ?? Array.Empty<string>());
            var error = string.Join("\r\n", result.ErrorOutput ?? Array.Empty<string>());
            var textareaStyle = "width: 100%; height: 200px; white-space: pre;";

            builder.AddMarkupContent(0, "<h5>Input</h5>");
            builder.AddMarkupContent(1, $"<textarea readonly style=\"{textareaStyle}\">{result.ScriptInput}</textarea>");
            builder.AddMarkupContent(2, "<h5 class=\"mt-3\">Standard Output</h5>");
            builder.AddMarkupContent(3, $"<textarea readonly style=\"{textareaStyle}\">{output}</textarea>");
            builder.AddMarkupContent(4, "<h5 class=\"mt-3\">Error Output</h5>");
            builder.AddMarkupContent(3, $"<textarea readonly style=\"{textareaStyle}\">{error}</textarea>");
        }

        ModalService.ShowModal("Script Input/Output", outputModal);
    }

    private string GetProgressMessage(string key)
    {
        if (_fileUploadProgressLookup.TryGetValue(key, out var value))
        {
            return $"{MathHelper.GetFormattedPercent(value)} - {key}";
        }

        return string.Empty;
    }

    private async Task OnFileInputChanged(InputFileChangeEventArgs args)
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            EnsureUserSet();

            ToastService.ShowToast("File upload started.");

            if (args.File.Size > AppConstants.MaxUploadFileSize)
            {
                var maxFileSize = AppConstants.MaxUploadFileSize / 1000 / 1000;
                ToastService.ShowToast2($"File size exceeds the maximum allowed size of {maxFileSize}MB.", ToastType.Warning);
                return;
            }

            var fileId = await DataService.AddSharedFile(args.File, User.OrganizationID, OnFileInputProgress);
            var transferId = Guid.NewGuid().ToString();
            var result = await CircuitConnection.TransferFileFromBrowserToAgent(_device.ID, transferId, [fileId]);

            if (!result)
            {
                ToastService.ShowToast("Device not found.", classString: "bg-warning");
            }
            else
            {
                ToastService.ShowToast("File upload completed.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while uploading file to device.");
            ToastService.ShowToast2("Failed to upload file", ToastType.Error);
        }
        finally
        {
            if (args.File.Name is not null)
            {
                _ = _fileUploadProgressLookup.TryRemove(args.File.Name, out _);
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void OnFileInputProgress(double percentComplete, string fileName)
    {
        if (_fileUploadProgressLookup.TryGetValue(fileName, out var existingValue) &&
            percentComplete < 1 &&
            percentComplete - existingValue < .05)
        {
            // Avoid too frequent of updates.
            return;
        }
        _fileUploadProgressLookup.AddOrUpdate(fileName, percentComplete, (k, v) => percentComplete);
        InvokeAsync(StateHasChanged);
    }

    private void StartChat()
    {
        if (_device is null)
        {
            return;
        }

        var session = ChatCache.GetOrAdd(_device.ID, key =>
        {
            return new ChatSession()
            {
                DeviceId = key,
                DeviceName = _device.DeviceName,
                IsExpanded = true
            };
        });

        session.IsExpanded = true;
        Messenger.Send(new ChatSessionsChangedMessage(), CircuitConnection.ConnectionId);
    }

    private async Task ShowTaskManager()
    {
        if (_device is null)
        {
            return;
        }

        if (!ServiceSessionCache.TryGetConnectionId(_device.ID, out _))
        {
            ToastService.ShowToast("Device connection not found", classString: "bg-danger");
            return;
        }

        void showModal(RenderTreeBuilder builder)
        {
            builder.OpenComponent<TaskManagerModal>(0);
            builder.AddAttribute(1, "DeviceId", _device.ID);
            builder.AddAttribute(2, "DeviceName", _device.DeviceName);
            builder.AddAttribute(3, "Platform", _device.Platform);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Task Manager - {_device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowScreenshots()
    {
        if (_device is null)
        {
            return;
        }

        void showModal(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ScreenshotModal>(0);
            builder.AddAttribute(1, "DeviceId", _device.ID);
            builder.AddAttribute(2, "DeviceName", _device.DeviceName);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Screenshots - {_device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowCamera()
    {
        if (_device is null)
        {
            return;
        }

        void showModal(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CameraModal>(0);
            builder.AddAttribute(1, "DeviceId", _device.ID);
            builder.AddAttribute(2, "DeviceName", _device.DeviceName);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Camera - {_device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowAudio()
    {
        if (_device is null)
        {
            return;
        }

        void showModal(RenderTreeBuilder builder)
        {
            builder.OpenComponent<AudioModal>(0);
            builder.AddAttribute(1, "DeviceId", _device.ID);
            builder.AddAttribute(2, "DeviceName", _device.DeviceName);
            var capabilities = DesktopCapabilityMatrix.For(_device);
            builder.AddAttribute(3, "CanRecordAudio",
                IsFeatureAvailable(capabilities.AudioRecording));
            builder.AddAttribute(4, "CanListenLive",
                IsFeatureAvailable(capabilities.LiveAudio));
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Audio - {_device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowKeyboard()
    {
        if (_device is null)
        {
            return;
        }

        void showModal(RenderTreeBuilder builder)
        {
            builder.OpenComponent<KeyboardModal>(0);
            builder.AddAttribute(1, "DeviceId", _device.ID);
            builder.AddAttribute(2, "DeviceName", _device.DeviceName);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Keyboard - {_device.DeviceName}", showModal, size: "lg");
    }

    private async Task StartRemoteControl(bool viewOnly)
    {
        if (_device is null)
        {
            return;
        }

        if (!ServiceSessionCache.TryGetConnectionId(_device.ID, out _))
        {
            ToastService.ShowToast("Device connection not found", classString: "bg-danger");
            return;
        }

        var result = await CircuitConnection.RemoteControl(_device.ID, viewOnly);
        if (!result.IsSuccess)
        {
            return;
        }

        var session = result.Value;

        if (!await session.WaitForSessionReady(TimeSpan.FromSeconds(20)))
        {
            ToastService.ShowToast("Session failed to start", classString: "bg-danger");
            return;
        }

        JsInterop.OpenWindow(
            $"/Viewer" +
                $"?mode=Unattended&sessionId={session.UnattendedSessionId}" +
                $"&accessKey={session.AccessKey}" +
                $"&viewonly={viewOnly}",
            "_blank");
    }

    private async Task UninstallAgent()
    {
        if (_device is null)
        {
            return;
        }

        var result = await JsInterop.Confirm("Are you sure you want to uninstall this agent?  This is permanent!");
        if (result)
        {
            await CircuitConnection.UninstallAgents(new[] { _device.ID });
            NavManager.NavigateTo("/");
        }
    }

    private async Task UpdateAgent()
    {
        if (_device is null)
        {
            return;
        }

        if (!ServiceSessionCache.TryGetConnectionId(_device.ID, out _))
        {
            ToastService.ShowToast("Device is offline.  It will update itself when it reconnects.", classString: "bg-warning");
            return;
        }

        var updated = await CircuitConnection.UpdateAgents(new[] { _device.ID });
        ToastService.ShowToast(updated > 0 ? "Update command sent." : "Device is offline.  It will update itself when it reconnects.", classString: updated > 0 ? string.Empty : "bg-warning");
    }

    private async Task WakeDevice()
    {
        if (_device is null)
        {
            return;
        }

        var result = await CircuitConnection.WakeDevice(_device);
        if (result.IsSuccess)
        {
            ToastService.ShowToast2(
                $"Wake command sent to peer devices.",
                ToastType.Success);
        }
        else
        {
            ToastService.ShowToast2(
                $"Wake command failed.  Reason: {result.Reason}",
                ToastType.Error);
        }
    }
}