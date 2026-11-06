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
/// Records audio clips from the system audio or the microphone of a remote
/// device, shows the clips that were recorded before, so they can be played
/// and deleted, and streams the audio of the device while it is listened to.
/// </summary>
public partial class AudioModal : AuthComponentBase, IDisposable
{
    /// <summary>
    /// The number of clips that are shown at most.
    /// </summary>
    private const int MaxClips = 100;

    private static readonly QualityOption[] _qualityOptions = BuildQualityOptions();

    private readonly List<AudioDeviceInfo> _microphones = new();
    private readonly List<AudioClip> _clips = new();
    private readonly List<ScreenshotPauseWindow> _pauseWindows = new();
    private string? _errorMessage;
    private string _selectedMicrophoneId = string.Empty;
    private string _liveMicrophoneId = string.Empty;
    private string _storageDirectory = string.Empty;
    private string _directory = string.Empty;
    private AudioSource _source = AudioSource.SystemAudio;
    private AudioSource _liveSource = AudioSource.SystemAudio;
    private int _quality = (int)AudioQuality.Medium;
    private int _durationSeconds = AudioQualityPresets.DefaultDurationSeconds;
    private int _intervalMinutes = 5;
    private int _retentionCount = 200;
    private int _retentionHours = 24;
    private bool _isBusy;
    private bool _isMonitoring;
    private bool _isLive;
    private bool _showFolderPicker;

    [Parameter]
    public string DeviceId { get; set; } = string.Empty;

    [Parameter]
    public string DeviceName { get; set; } = string.Empty;

    [Inject]
    public required ICircuitConnection CircuitConnection { get; init; }

    [Inject]
    public required IAudioService AudioService { get; init; }

    [Inject]
    public required IDataService DataService { get; init; }

    [Inject]
    public required IToastService ToastService { get; init; }

    [Inject]
    public required IJsInterop JsInterop { get; init; }

    [Inject]
    public required ILogger<AudioModal> Logger { get; init; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();

        await Register<AudioDevicesReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleAudioDevicesReceivedMessage);

        await Register<AudioClipReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleAudioClipReceivedMessage);

        await Register<AudioLiveChunkMessage, string>(
            CircuitConnection.ConnectionId,
            HandleAudioLiveChunkMessage);

        await LoadDeviceSettings();
        LoadMonitoringStatus();

        LoadMicrophones();
        LoadClips();
    }

    public new void Dispose()
    {
        try
        {
            JsInterop.StopAudioPlayback();
        }
        catch
        {
            // The browser is already gone.
        }

        base.Dispose();
    }

    /// <summary>
    /// Builds the label of every quality level from the preset that belongs
    /// to it, so the sample rate is always the one that the device is going
    /// to use.
    /// </summary>
    private static QualityOption[] BuildQualityOptions()
    {
        var options = new List<QualityOption>();

        foreach (var quality in new[] { AudioQuality.Low, AudioQuality.Medium, AudioQuality.High })
        {
            var preset = AudioQualityPresets.GetPreset(quality);
            options.Add(new QualityOption(
                (int)quality,
                $"{quality} ({preset.SampleRate / 1000} kHz)"));
        }

        return options.ToArray();
    }

    private string GetClipUrl(string fileName)
    {
        return $"api/audioClips/{DeviceId}/{fileName}?t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
    }

    private static string GetSourceName(string source)
    {
        return Enum.TryParse<AudioSource>(source, ignoreCase: true, out var parsed) ?
            parsed.ToString() :
            "Unknown";
    }

    /// <summary>
    /// Reads the microphones that the device reported the last time it was
    /// asked to record.
    /// </summary>
    private void LoadMicrophones()
    {
        var microphones = CircuitConnection.GetAudioDevices(DeviceId);

        _microphones.Clear();
        _microphones.AddRange(microphones);

        SelectFirstMicrophoneIfNeeded();
    }

    /// <summary>
    /// Makes sure that a microphone is selected whenever there is one, so the
    /// recording doesn't start without one.
    /// </summary>
    private void SelectFirstMicrophoneIfNeeded()
    {
        if (_microphones.Count == 0)
        {
            _selectedMicrophoneId = string.Empty;
            _liveMicrophoneId = string.Empty;
            return;
        }

        if (!_microphones.Any(x => string.Equals(x.Id, _selectedMicrophoneId, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedMicrophoneId = _microphones[0].Id;
        }

        if (!_microphones.Any(x => string.Equals(x.Id, _liveMicrophoneId, StringComparison.OrdinalIgnoreCase)))
        {
            _liveMicrophoneId = _microphones[0].Id;
        }
    }

    private void LoadClips()
    {
        var clips = AudioService.GetClips(DeviceId, MaxClips);

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
        _storageDirectory = AudioService.GetAudioDirectory(device);
        _directory = device.AudioDirectory ?? _storageDirectory;
        _retentionCount = device.AudioRetentionCount > 0 ?
            device.AudioRetentionCount :
            AudioService.DefaultRetentionCount;
        _retentionHours = device.AudioRetentionHours > 0 ?
            device.AudioRetentionHours :
            AudioService.DefaultRetentionHours;

        _pauseWindows.Clear();
        _pauseWindows.AddRange(ScreenshotPause.Deserialize(device.AudioPauseWindows));
    }

    private void LoadMonitoringStatus()
    {
        var status = CircuitConnection.GetAudioMonitoringStatus(DeviceId);
        _isMonitoring = status is not null;

        if (status is null)
        {
            return;
        }

        _intervalMinutes = status.IntervalMinutes;
        _source = status.CaptureRequest.Source;
        _selectedMicrophoneId = status.CaptureRequest.MicrophoneId;
        _quality = (int)status.CaptureRequest.Quality;
        _durationSeconds = status.CaptureRequest.DurationSeconds;
    }

    private async Task HandleAudioDevicesReceivedMessage(object subscriber, AudioDevicesReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _microphones.Clear();

            if (message.Devices is not null)
            {
                _microphones.AddRange(message.Devices);
            }

            SelectFirstMicrophoneIfNeeded();

            StateHasChanged();
        });
    }

    private async Task HandleAudioClipReceivedMessage(object subscriber, AudioClipReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            LoadClips();

            ToastService.ShowToast2(
                $"An audio clip was received from {DeviceName}.",
                ToastType.Success);

            StateHasChanged();
        });
    }

    private async Task HandleAudioLiveChunkMessage(object subscriber, AudioLiveChunkMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // The message arrives on the thread of the hub, so the work is moved
        // to the circuit of the browser before the JavaScript is called.
        try
        {
            await InvokeAsync(async () =>
            {
                try
                {
                    await JsInterop.PlayAudioChunk(Convert.ToBase64String(message.Chunk));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error while playing a live audio chunk.");
                }
            });
        }
        catch (Exception ex)
        {
            // The modal was closed while the chunk was on its way.
            Logger.LogWarning(ex, "A live audio chunk could not be delivered.");
        }
    }

    /// <summary>
    /// Asks the device to record a clip with the selected source, quality and
    /// duration.
    /// </summary>
    private async Task RecordClip()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            // The device only records for the durations that are allowed.
            _durationSeconds = AudioQualityPresets.ClampDurationSeconds(_durationSeconds);

            var result = await CircuitConnection.StartAudioCapture(
                DeviceId,
                _source,
                _selectedMicrophoneId,
                (int)_quality,
                _durationSeconds);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2(
                $"Recording a {_durationSeconds} second audio clip from {DeviceName}...",
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
            _durationSeconds = AudioQualityPresets.ClampDurationSeconds(_durationSeconds);
            var result = await CircuitConnection.StartAudioMonitoring(
                DeviceId,
                _source,
                _selectedMicrophoneId,
                (int)_quality,
                _durationSeconds,
                _intervalMinutes);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = true;
            ToastService.ShowToast2($"Scheduled audio recording started for {DeviceName}.", ToastType.Success);
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
            var result = await CircuitConnection.StopAudioMonitoring(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = false;
            ToastService.ShowToast2($"Scheduled audio recording stopped for {DeviceName}.", ToastType.Info);
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

    private async Task StartLive()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StartAudioLive(
                DeviceId,
                _liveSource,
                _liveMicrophoneId);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isLive = true;
            ToastService.ShowToast2($"Listening to the audio of {DeviceName}.", ToastType.Success);
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

    private async Task StopLive()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StopAudioLive(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isLive = false;
            ToastService.ShowToast2($"Stopped listening to {DeviceName}.", ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            try
            {
                await JsInterop.StopAudioPlayback();
            }
            catch
            {
                // The browser is already gone.
            }

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
            var result = await CircuitConnection.UpdateAudioSettings(
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
            ToastService.ShowToast2("Audio settings saved.", ToastType.Success);
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
            var result = await CircuitConnection.StopAudioCapture(DeviceId);
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
    /// Asks the device for the microphones that it has.  A duration of zero
    /// only requests the list of microphones, so nothing is recorded.
    /// </summary>
    private async Task RefreshMicrophones()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StartAudioCapture(
                DeviceId,
                _source,
                _selectedMicrophoneId,
                (int)_quality,
                0);

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2(
                $"Asked {DeviceName} for its microphones.",
                ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            LoadMicrophones();
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
            LoadMicrophones();
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

    private async Task DeleteClip(AudioClip clip)
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.DeleteAudioClip(DeviceId, clip.FileName);
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
            "Are you sure you want to delete every audio clip of this device?");

        if (!confirmed)
        {
            return;
        }

        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.ClearAudioClips(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _clips.Clear();

            ToastService.ShowToast2(
                "All audio clips were deleted.",
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
