using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
using Remotely.Shared.Enums;
using Remotely.Shared.Utilities;
using Remotely.Shared.ViewModels;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Remotely.Server.Components.Devices;

public partial class DeviceCard : AuthComponentBase, IDisposable
{
    private readonly ConcurrentDictionary<string, double> _fileUploadProgressLookup = new();
    private ElementReference _card;
    private Version _currentVersion = new();
    private Theme _theme;
    private DeviceCardState _state;
    private DeviceGroup[] _deviceGroups = Array.Empty<DeviceGroup>();

    private CancellationTokenSource? _previewCts;
    private string? _previewFileName;
    private bool _previewAutoEnabled = true;
    private int _previewRefreshSeconds = 15;
    private DateTimeOffset? _previewTimestamp;

    [Parameter]
    public Device Device { get; set; } = null!;

    [CascadingParameter]
    public DevicesFrame ParentFrame { get; init; } = null!;


    [Inject]
    public required ISelectedCardsStore SelectedCards { get; init; }

    [Inject]
    public required IThemeProvider ThemeProvider { get; init; }

    [Inject]
    public required ICircuitConnection CircuitConnection { get; init; }

    [Inject]
    public required IDataService DataService { get; init; }

    [Inject]
    public required IChatSessionStore ChatCache { get; init; }

    [Inject]
    public required ILogger<DeviceCard> Logger { get; init; }

    private bool IsExpanded => _state == DeviceCardState.Expanded;

    private bool IsOutdated =>
        Version.TryParse(Device.AgentVersion, out var result) &&
        result < _currentVersion;

    private bool IsSelected => SelectedCards.SelectedDevices.Contains(Device.ID);

    private DesktopCapabilities DeviceCapabilities =>
        DesktopCapabilityMatrix.For(Device);

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

    [Inject]
    private IJsInterop JsInterop { get; init; } = null!;

    [Inject]
    private IModalService ModalService { get; init; } = null!;

    [Inject]
    private IAgentHubSessionCache ServiceSessionCache { get; init; } = null!;

    [Inject]
    private IToastService ToastService { get; init; } = null!;

    [Inject]
    private IUpgradeService UpgradeService { get; init; } = null!;

    [Inject]
    private IScreenshotService ScreenshotService { get; init; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();
        _theme = await ThemeProvider.GetEffectiveTheme();
        _currentVersion = UpgradeService.GetCurrentVersion();
        _deviceGroups = DataService.GetDeviceGroups(UserName);

        await Register<DeviceCardStateChangedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleDeviceCardStateChanged);

        await Register<DeviceStateChangedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleDeviceStateChanged);
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
        _previewFileName is null
            ? null
            : $"api/screenshots/{Device.ID}/{_previewFileName}?t={_previewTimestamp?.ToUnixTimeMilliseconds() ?? DateTimeOffset.Now.ToUnixTimeMilliseconds()}";

    private async Task RefreshPreviewOnce()
    {
        var result = await CircuitConnection.CaptureScreenshotNow(Device.ID);
        if (!result.IsSuccess)
        {
            // Offline, paused by pause windows, and so on. The tile shows
            // the last screenshot that arrived, if any.
            return;
        }

        var screenshots = ScreenshotService.GetScreenshots(Device.ID, 1);
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
            // The card was collapsed or unmounted.
        }
    }

    private void StopPreviewLoop()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
    }

    private async Task HandleDeviceCardStateChanged(object subscriber, DeviceCardStateChangedMessage message)
    {
        if (message.DeviceId == Device.ID)
        {
            if (message.State == _state)
            {
                return;
            }
            _state = message.State;

            if (_state == DeviceCardState.Expanded)
            {
                StartPreviewLoop();
            }
            else
            {
                StopPreviewLoop();
            }

            await InvokeAsync(StateHasChanged);
        }
        else
        {
            if (_state == DeviceCardState.Normal)
            {
                return;
            }
            _state = DeviceCardState.Normal;
            StopPreviewLoop();
            await InvokeAsync(StateHasChanged);
            return;
        }
    }

    private async Task HandleDeviceStateChanged(object subscriber, DeviceStateChangedMessage message)
    {
        if (message.Device.ID != Device.ID)
        {
            return;
        }

        // TODO: It would be cool to decorate user-editable properties
        // with a "UserEditable" attribute, then use a source generator
        // to create/update a method that copies property values for
        // those that do not have the attribute.  We could do the same
        // with reflection, but this method is called too frequently,
        // and the performance hit would likely be significant.

        // If the card is expanded, only update the immutable UI
        // elements, so any changes to the form fields aren't lost.
        if (IsExpanded)
        {
            Device.CurrentUser = message.Device.CurrentUser;
            Device.Platform = message.Device.Platform;
            Device.TotalStorage = message.Device.TotalStorage;
            Device.UsedStorage = message.Device.UsedStorage;
            Device.Drives = message.Device.Drives;
            Device.CpuUtilization = message.Device.CpuUtilization;
            Device.TotalMemory = message.Device.TotalMemory;
            Device.UsedMemory = message.Device.UsedMemory;
            Device.AgentVersion = message.Device.AgentVersion;
            Device.LastOnline = message.Device.LastOnline;
            Device.PublicIP = message.Device.PublicIP;
            Device.MacAddresses = message.Device.MacAddresses;
        }
        else
        {
            Device = message.Device;
        }
        await InvokeAsync(StateHasChanged);
    }

    private void ContextMenuOpening(MouseEventArgs args)
    {
        if (_state == DeviceCardState.Normal)
        {
            JsInterop.OpenWindow($"/device-details/{Device.ID}", "_blank");
        }
    }

    private async Task ExpandCard(MouseEventArgs args)
    {
        await ExpandCardCore();
    }

    private async Task ExpandCardCore()
    {
        if (_state == DeviceCardState.Expanded)
        {
            return;
        }

        await Messenger.Send(
            new DeviceCardStateChangedMessage(Device.ID, DeviceCardState.Expanded),
            CircuitConnection.ConnectionId);

        JsInterop.ScrollToElement(_card);

        await CircuitConnection.TriggerHeartbeat(Device.ID);
    }


    private string GetCardStateClass()
    {
        return $"{_state}".ToLower();
    }

    private string GetStatusClass() =>
        Device.IsOnline
            ? IsOutdated ? "is-warn" : "is-online"
            : "is-offline";

    private string GetStatusTitle() =>
        Device.IsOnline
            ? IsOutdated ? "Online - agent outdated" : "Online"
            : "Offline";

    private static string GetGaugeClass(double ratio) =>
        ratio switch
        {
            >= 0.9 => "is-critical",
            >= 0.8 => "is-warn",
            _ => string.Empty
        };

    private static string GetGaugeWidth(double ratio) =>
        $"{Math.Clamp(ratio, 0, 1) * 100:0.#}%";

    private static string DisplayOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "\u2014" : value;

    private string FormatLastOnline() =>
        Device.LastOnline == default
            ? "\u2014"
            : Device.LastOnline.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private async Task HandleCardKeyDown(KeyboardEventArgs args)
    {
        if (_state != DeviceCardState.Normal)
        {
            return;
        }

        if (args.Key is "Enter" or " ")
        {
            await ExpandCardCore();
        }
    }

    private string GetProgressMessage(string key)
    {
        if (_fileUploadProgressLookup.TryGetValue(key, out var value))
        {
            return $"{MathHelper.GetFormattedPercent(value)} - {key}";
        }

        return string.Empty;
    }

    private void HandleHeaderClick()
    {
        if (IsExpanded)
        {
            _state = DeviceCardState.Normal;
        }
    }
    private async Task HandleValidSubmit()
    {
        EnsureUserSet();
        if (!DataService.DoesUserHaveAccessToDevice(Device.ID, User))
        {
            ToastService.ShowToast("Unauthorized.", classString: "bg-warning");
            return;
        }
       
        await DataService.UpdateDevice(Device.ID,
              Device.Tags,
              Device.Alias,
              Device.DeviceGroupID,
              Device.Notes);

        ToastService.ShowToast("Device settings saved.");

        await CircuitConnection.TriggerHeartbeat(Device.ID);
    }

    private async Task OnFileInputChanged(InputFileChangeEventArgs args)
    {
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
            var result = await CircuitConnection.TransferFileFromBrowserToAgent(Device.ID, transferId, [fileId]);

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

    private void OpenDeviceDetails()
    {
        JsInterop.OpenWindow($"/device-details/{Device.ID}", "_blank");
    }

    private void ShowAllDisks()
    {
        var disksString = JsonSerializer.Serialize(Device.Drives, JsonSerializerHelper.IndentedOptions);

        void modalBody(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0, $"<div style='white-space: pre'>{disksString}</div>");
        }

        ModalService.ShowModal($"All Disks for {Device.DeviceName}", modalBody);
    }

    private void StartChat()
    {
        var session = ChatCache.GetOrAdd(Device.ID, key =>
        {
            return new ChatSession()
            {
                DeviceId = key,
                DeviceName = Device.DeviceName,
                IsExpanded = true
            };
        });

        session.IsExpanded = true;
        Messenger.Send(new ChatSessionsChangedMessage(), CircuitConnection.ConnectionId);
    }

    private async Task ShowTaskManager()
    {
        if (!ServiceSessionCache.TryGetConnectionId(Device.ID, out _))
        {
            ToastService.ShowToast("Device connection not found", classString: "bg-danger");
            return;
        }

        void showModal(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<TaskManagerModal>(0);
            builder.AddAttribute(1, "DeviceId", Device.ID);
            builder.AddAttribute(2, "DeviceName", Device.DeviceName);
            builder.AddAttribute(3, "Platform", Device.Platform);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Task Manager - {Device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowScreenshots()
    {
        void showModal(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<ScreenshotModal>(0);
            builder.AddAttribute(1, "DeviceId", Device.ID);
            builder.AddAttribute(2, "DeviceName", Device.DeviceName);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Screenshots - {Device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowCamera()
    {
        void showModal(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<CameraModal>(0);
            builder.AddAttribute(1, "DeviceId", Device.ID);
            builder.AddAttribute(2, "DeviceName", Device.DeviceName);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Camera - {Device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowAudio()
    {
        void showModal(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<AudioModal>(0);
            builder.AddAttribute(1, "DeviceId", Device.ID);
            builder.AddAttribute(2, "DeviceName", Device.DeviceName);
            var capabilities = DesktopCapabilityMatrix.For(Device);
            builder.AddAttribute(3, "CanRecordAudio",
                IsFeatureAvailable(capabilities.AudioRecording));
            builder.AddAttribute(4, "CanListenLive",
                IsFeatureAvailable(capabilities.LiveAudio));
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Audio - {Device.DeviceName}", showModal, size: "lg");
    }

    private async Task ShowKeyboard()
    {
        void showModal(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<KeyboardModal>(0);
            builder.AddAttribute(1, "DeviceId", Device.ID);
            builder.AddAttribute(2, "DeviceName", Device.DeviceName);
            builder.CloseComponent();
        }

        await ModalService.ShowModal($"Keyboard - {Device.DeviceName}", showModal, size: "lg");
    }

    private async Task StartRemoteControl(bool viewOnly)
    {
        if (!ServiceSessionCache.TryGetConnectionId(Device.ID, out _))
        {
            ToastService.ShowToast("Device connection not found", classString: "bg-danger");
            return;
        }

        var result = await CircuitConnection.RemoteControl(Device.ID, viewOnly);
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

    private void ToggleIsSelected(ChangeEventArgs args)
    {
        if (args.Value is not bool isSelected)
        {
            return;
        }

        if (isSelected)
        {
            SelectedCards.Add(Device.ID);
        }
        else
        {
            SelectedCards.Remove(Device.ID);
        }
    }

    private async Task UninstallAgent()
    {
        var result = await JsInterop.Confirm("Are you sure you want to uninstall this agent?  This is permanent!");
        if (result)
        {
            await CircuitConnection.UninstallAgents(new[] { Device.ID });
            _state = DeviceCardState.Normal;
            await ParentFrame.Refresh();
        }
    }

    private async Task UpdateAgent()
    {
        if (!ServiceSessionCache.TryGetConnectionId(Device.ID, out _))
        {
            ToastService.ShowToast("Device is offline.  It will update itself when it reconnects.", classString: "bg-warning");
            return;
        }

        var updated = await CircuitConnection.UpdateAgents(new[] { Device.ID });
        ToastService.ShowToast(updated > 0 ? "Update command sent." : "Device is offline.  It will update itself when it reconnects.", classString: updated > 0 ? string.Empty : "bg-warning");
    }

    private async Task WakeDevice()
    {
        var result = await CircuitConnection.WakeDevice(Device);
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
