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
/// Records video clips from the cameras or the displays of a remote device,
/// optionally with the system audio or a microphone, shows the clips that
/// were recorded before, so they can be played and deleted.
/// </summary>
public partial class CameraModal : AuthComponentBase, IDisposable
{
    /// <summary>
    /// The number of clips that are shown at most.
    /// </summary>
    private const int MaxClips = 100;

    private static readonly QualityOption[] _qualityOptions = BuildQualityOptions();

    private readonly List<CameraDeviceInfo> _videoSources = new();
    private readonly List<CameraAudioDeviceInfo> _audioDevices = new();
    private readonly List<CameraClip> _clips = new();
    private readonly List<ScreenshotPauseWindow> _pauseWindows = new();
    private string? _errorMessage;
    private string _selectedSource = string.Empty;
    private string _selectedAudio = $"{CameraAudioSource.None}|";
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

        LoadSources();
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
    /// The value of a video source in the selector.  The kind is part of it,
    /// so a camera and a display can never be mixed up.
    /// </summary>
    private static string GetSourceValue(CameraDeviceInfo source)
    {
        return CameraSourceSelection.GetSourceValue(source);
    }

    private static string GetSourceLabel(CameraDeviceInfo source)
    {
        return source.Kind == CameraDeviceKind.Display ?
            source.Name :
            $"Camera - {source.Name}";
    }

    /// <summary>
    /// The value of an audio source in the selector.  A null device means
    /// that the recording has no audio at all.
    /// </summary>
    private static string GetAudioValue(CameraAudioDeviceInfo? device)
    {
        return CameraSourceSelection.GetAudioValue(device);
    }

    private static string GetAudioLabel(CameraAudioDeviceInfo device)
    {
        return device.Source == CameraAudioSource.SystemAudio ?
            $"System audio - {device.Name}" :
            $"Microphone - {device.Name}";
    }

    private static (CameraDeviceKind Kind, string Id) ParseSource(string value)
    {
        return CameraSourceSelection.ParseSource(value);
    }

    private static (CameraAudioSource Source, string Id) ParseAudio(string value)
    {
        return CameraSourceSelection.ParseAudio(value);
    }

    /// <summary>
    /// Builds the request that is sent to the device with the source, the
    /// audio, the quality, and the duration that were selected.
    /// </summary>
    private CameraCaptureRequest BuildRequest(int durationSeconds)
    {
        var (kind, id) = ParseSource(_selectedSource);
        var (audioSource, audioId) = ParseAudio(_selectedAudio);

        return new CameraCaptureRequest(
            id,
            (CameraQuality)_quality,
            durationSeconds,
            kind,
            audioSource,
            audioId);
    }

    /// <summary>
    /// Reads the cameras and the displays that the device reported the last
    /// time it was asked to record.
    /// </summary>
    private void LoadSources()
    {
        var sources = CircuitConnection.GetCameraDevices(DeviceId);

        _videoSources.Clear();
        _videoSources.AddRange(sources);

        var audioDevices = CircuitConnection.GetCameraAudioDevices(DeviceId);

        _audioDevices.Clear();
        _audioDevices.AddRange(audioDevices);

        SelectFirstSourceIfNeeded();
        SelectFirstAudioDeviceIfNeeded();
    }

    /// <summary>
    /// Makes sure that a video source is selected whenever there is one, so
    /// the recording doesn't start without one.
    /// </summary>
    private void SelectFirstSourceIfNeeded()
    {
        if (_videoSources.Count == 0)
        {
            _selectedSource = string.Empty;
            return;
        }

        var current = ParseSource(_selectedSource);

        var selected = _videoSources.FirstOrDefault(x =>
            x.Kind == current.Kind &&
            string.Equals(x.Id, current.Id, StringComparison.OrdinalIgnoreCase));

        selected ??= _videoSources.FirstOrDefault(x => x.Kind == CameraDeviceKind.Display)
            ?? _videoSources[0];

        _selectedSource = GetSourceValue(selected);
    }

    /// <summary>
    /// Makes sure that the selected audio device is still one of the devices
    /// that the device reported.
    /// </summary>
    private void SelectFirstAudioDeviceIfNeeded()
    {
        var (source, id) = ParseAudio(_selectedAudio);

        if (source == CameraAudioSource.None)
        {
            return;
        }

        var selected = _audioDevices.FirstOrDefault(x =>
            x.Source == source &&
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));

        if (selected is not null)
        {
            return;
        }

        var replacement = _audioDevices.FirstOrDefault(x => x.Source == source);

        _selectedAudio = replacement is null ?
            $"{CameraAudioSource.None}|" :
            GetAudioValue(replacement);
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

        var request = status.CaptureRequest;
        _selectedSource = $"{request.VideoSource}|{request.CameraId}";
        _selectedAudio = request.AudioSource == CameraAudioSource.None ?
            $"{CameraAudioSource.None}|" :
            $"{request.AudioSource}|{request.AudioDeviceId}";
        _quality = (int)request.Quality;
        _durationSeconds = request.DurationSeconds;
    }

    private async Task HandleCameraDevicesReceivedMessage(object subscriber, CameraDevicesReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _videoSources.Clear();

            if (message.Cameras is not null)
            {
                _videoSources.AddRange(message.Cameras);
            }

            _audioDevices.Clear();

            if (message.AudioDevices is not null)
            {
                _audioDevices.AddRange(message.AudioDevices);
            }

            SelectFirstSourceIfNeeded();
            SelectFirstAudioDeviceIfNeeded();

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
                $"A video clip was received from {DeviceName}.",
                ToastType.Success);

            StateHasChanged();
        });
    }

    /// <summary>
    /// Asks the device to record a clip with the selected source, audio,
    /// quality and duration.
    /// </summary>
    private async Task RecordClip()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            if (string.IsNullOrWhiteSpace(_selectedSource))
            {
                _errorMessage = "Select a camera or a screen before recording.";
                return;
            }

            // The device only records for the durations that are allowed.
            _durationSeconds = CameraQualityPresets.ClampDurationSeconds(_durationSeconds);

            var result = await CircuitConnection.StartCameraCapture(
                DeviceId,
                BuildRequest(_durationSeconds));

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
            if (string.IsNullOrWhiteSpace(_selectedSource))
            {
                _errorMessage = "Select a camera or a screen before starting scheduled recording.";
                return;
            }

            _durationSeconds = CameraQualityPresets.ClampDurationSeconds(_durationSeconds);
            var result = await CircuitConnection.StartCameraMonitoring(
                DeviceId,
                BuildRequest(_durationSeconds),
                _intervalMinutes);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = true;
            ToastService.ShowToast2($"Scheduled recording started for {DeviceName}.", ToastType.Success);
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
            ToastService.ShowToast2($"Scheduled recording stopped for {DeviceName}.", ToastType.Info);
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
            ToastService.ShowToast2("Recording settings saved.", ToastType.Success);
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
    /// Asks the device for the cameras and the screens that it has.  A
    /// duration of zero only requests the list of sources, so nothing is
    /// recorded.
    /// </summary>
    private async Task RefreshSources()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StartCameraCapture(
                DeviceId,
                BuildRequest(0));

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2(
                $"Asked {DeviceName} for its cameras and screens.",
                ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            LoadSources();
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
            LoadSources();
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
            "Are you sure you want to delete every video clip of this device?");

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
                "All video clips were deleted.",
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
