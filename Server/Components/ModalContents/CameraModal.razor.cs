using Microsoft.AspNetCore.Components;
using Remotely.Server.Enums;
using Remotely.Server.Hubs;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Components.ModalContents;

/// <summary>
/// Records video clips from the cameras of a remote device, and shows the
/// clips that were recorded before, so they can be watched and deleted.
/// </summary>
public partial class CameraModal : AuthComponentBase, IDisposable
{
    /// <summary>
    /// The number of clips that are shown at most.
    /// </summary>
    private const int MaxClips = 100;

    private static readonly QualityOption[] _qualityOptions = BuildQualityOptions();

    private readonly List<CameraDeviceInfo> _cameras = new();
    private readonly List<CameraClip> _clips = new();
    private readonly List<ScreenshotPauseWindow> _pauseWindows = new();
    private string? _errorMessage;
    private string _selectedCameraId = string.Empty;
    private string _storageDirectory = string.Empty;
    private string _directory = string.Empty;
    private int _quality = (int)CameraQuality.Medium;
    private int _durationSeconds = CameraQualityPresets.DefaultDurationSeconds;
    private int _intervalMinutes = 5;
    private int _retentionCount = 200;
    private int _retentionHours = 24;
    private bool _isBusy;
    private bool _isMonitoring;
    private bool _showFolderPicker;

    [Parameter]
    public string DeviceId { get; set; } = string.Empty;

    [Parameter]
    public string DeviceName { get; set; } = string.Empty;

    [Inject]
    public required ICircuitConnection CircuitConnection { get; init; }

    [Inject]
    public required ICameraService CameraService { get; init; }

    [Inject]
    public required IDataService DataService { get; init; }

    [Inject]
    public required IToastService ToastService { get; init; }

    [Inject]
    public required IJsInterop JsInterop { get; init; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();

        await Register<CameraDevicesReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleCameraDevicesReceivedMessage);

        await Register<CameraClipReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleCameraClipReceivedMessage);

        await LoadDeviceSettings();
        LoadMonitoringStatus();

        LoadCameras();
        LoadClips();
    }

    public new void Dispose()
    {
        base.Dispose();
    }

    /// <summary>
    /// Builds the label of every quality level from the preset that belongs
    /// to it, so the resolution and the frame rate are always the ones that
    /// the device is going to use.
    /// </summary>
    private static QualityOption[] BuildQualityOptions()
    {
        var options = new List<QualityOption>();

        foreach (var quality in new[] { CameraQuality.Low, CameraQuality.Medium, CameraQuality.High })
        {
            var preset = CameraQualityPresets.GetPreset(quality);
            options.Add(new QualityOption(
                (int)quality,
                $"{quality} ({preset.Width}x{preset.Height} @{preset.Framerate} fps)"));
        }

        return options.ToArray();
    }

    private string GetClipUrl(string fileName)
    {
        return $"api/cameras/{DeviceId}/{fileName}?t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
    }

    /// <summary>
    /// Reads the cameras that the device reported the last time it was asked
    /// to record.
    /// </summary>
    private void LoadCameras()
    {
        var cameras = CircuitConnection.GetCameraDevices(DeviceId);

        _cameras.Clear();
        _cameras.AddRange(cameras);

        SelectFirstCameraIfNeeded();
    }

    /// <summary>
    /// Makes sure that a camera is selected whenever there is one, so the
    /// recording doesn't start without a camera.
    /// </summary>
    private void SelectFirstCameraIfNeeded()
    {
        if (_cameras.Count == 0)
        {
            _selectedCameraId = string.Empty;
            return;
        }

        if (!_cameras.Any(x => string.Equals(x.Id, _selectedCameraId, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedCameraId = _cameras[0].Id;
        }
    }

    private void LoadClips()
    {
        var clips = CameraService.GetClips(DeviceId, MaxClips);

        _clips.Clear();
        _clips.AddRange(clips);
    }

    private async Task LoadDeviceSettings()
    {
        var deviceResult = await DataService.GetDevice(DeviceId);
        if (!deviceResult.IsSuccess)
        {
            return;
        }

        var device = deviceResult.Value;
        _storageDirectory = CameraService.GetCameraDirectory(device);
        _directory = device.CameraDirectory ?? _storageDirectory;
        _retentionCount = device.CameraRetentionCount > 0 ?
            device.CameraRetentionCount :
            CameraService.DefaultRetentionCount;
        _retentionHours = device.CameraRetentionHours > 0 ?
            device.CameraRetentionHours :
            CameraService.DefaultRetentionHours;

        _pauseWindows.Clear();
        _pauseWindows.AddRange(ScreenshotPause.Deserialize(device.CameraPauseWindows));
    }

    private void LoadMonitoringStatus()
    {
        var status = CircuitConnection.GetCameraMonitoringStatus(DeviceId);
        _isMonitoring = status is not null;

        if (status is null)
        {
            return;
        }

        _intervalMinutes = status.IntervalMinutes;
        _selectedCameraId = status.CaptureRequest.CameraId;
        _quality = (int)status.CaptureRequest.Quality;
        _durationSeconds = status.CaptureRequest.DurationSeconds;
    }

    private async Task HandleCameraDevicesReceivedMessage(object subscriber, CameraDevicesReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _cameras.Clear();

            if (message.Cameras is not null)
            {
                _cameras.AddRange(message.Cameras);
            }

            SelectFirstCameraIfNeeded();

            StateHasChanged();
        });
    }

    private async Task HandleCameraClipReceivedMessage(object subscriber, CameraClipReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            LoadClips();

            ToastService.ShowToast2(
                $"A camera clip was received from {DeviceName}.",
                ToastType.Success);

            StateHasChanged();
        });
    }

    /// <summary>
    /// Asks the device to record a clip with the selected camera, quality
    /// and duration.
    /// </summary>
    private async Task RecordClip()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            if (string.IsNullOrWhiteSpace(_selectedCameraId))
            {
                _errorMessage = "Select a camera before recording.";
                return;
            }

            // The device only records for the durations that are allowed.
            _durationSeconds = CameraQualityPresets.ClampDurationSeconds(_durationSeconds);

            var result = await CircuitConnection.StartCameraCapture(
                DeviceId,
                _selectedCameraId,
                (int)_quality,
                _durationSeconds);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2(
                $"Recording a {_durationSeconds} second clip from {DeviceName}...",
                ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task StartMonitoring()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            if (string.IsNullOrWhiteSpace(_selectedCameraId))
            {
                _errorMessage = "Select a camera before starting scheduled recording.";
                return;
            }

            _durationSeconds = CameraQualityPresets.ClampDurationSeconds(_durationSeconds);
            var result = await CircuitConnection.StartCameraMonitoring(
                DeviceId,
                _selectedCameraId,
                _quality,
                _durationSeconds,
                _intervalMinutes);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = true;
            ToastService.ShowToast2($"Scheduled camera recording started for {DeviceName}.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task StopMonitoring()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StopCameraMonitoring(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = false;
            ToastService.ShowToast2($"Scheduled camera recording stopped for {DeviceName}.", ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private void AddPauseWindow()
    {
        _pauseWindows.Add(new ScreenshotPauseWindow(22, 7));
    }

    private void RemovePauseWindow(int index)
    {
        if (index >= 0 && index < _pauseWindows.Count)
        {
            _pauseWindows.RemoveAt(index);
        }
    }

    private async Task SaveSettings()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.UpdateCameraSettings(
                DeviceId,
                _directory,
                _retentionCount,
                _retentionHours,
                _pauseWindows.Count == 0 ? null : ScreenshotPause.Serialize(_pauseWindows));

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            await LoadDeviceSettings();
            LoadClips();
            ToastService.ShowToast2("Camera settings saved.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task FolderSelected(string path)
    {
        _directory = path;
        _showFolderPicker = false;
        await SaveSettings();
    }

    /// <summary>
    /// Stops the recording that is running on the device.
    /// </summary>
    private async Task StopRecording()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StopCameraCapture(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2(
                $"The recording on {DeviceName} was stopped.",
                ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Asks the device for the cameras that it has.  A duration of zero only
    /// requests the list of cameras, so nothing is recorded.
    /// </summary>
    private async Task RefreshCameras()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StartCameraCapture(
                DeviceId,
                string.Empty,
                (int)_quality,
                0);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2(
                $"Asked {DeviceName} for its cameras.",
                ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            LoadCameras();
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task RefreshAsync()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            LoadCameras();
            LoadClips();
            await LoadDeviceSettings();
            LoadMonitoringStatus();
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteClip(CameraClip clip)
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.DeleteCameraClip(DeviceId, clip.FileName);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            LoadClips();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task ClearClips()
    {
        var confirmed = await JsInterop.Confirm(
            "Are you sure you want to delete every camera clip of this device?");

        if (!confirmed)
        {
            return;
        }

        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.ClearCameraClips(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _clips.Clear();

            ToastService.ShowToast2(
                "All camera clips were deleted.",
                ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private sealed record QualityOption(int Value, string Label);
}
