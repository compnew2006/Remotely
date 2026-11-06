using Microsoft.AspNetCore.Components;
using Remotely.Server.Enums;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Components.ModalContents;

/// <summary>
/// Shows the screenshots captured from a device by the monitoring feature, and
/// lets the user start and stop the monitoring, choose where the screenshots
/// are stored, and delete them.
/// </summary>
public partial class ScreenshotModal : AuthComponentBase, IDisposable
{
    private readonly List<Screenshot> _screenshots = new();
    private string? _errorMessage;
    private string? _latestFileName;
    private DateTimeOffset _latestTimeStamp;
    private bool _isMonitoring;
    private bool _isBusy;
    private int _intervalMinutes = 5;
    private int _maxPerDevice = 200;
    private int _retentionCount = 200;
    private int _retentionHours = 24;
    private readonly List<ScreenshotPauseWindow> _pauseWindows = new();
    private string _directory = string.Empty;
    private bool _showFolderPicker;

    [Parameter]
    public string DeviceId { get; set; } = string.Empty;

    [Parameter]
    public string DeviceName { get; set; } = string.Empty;

    [Inject]
    public required ICircuitConnection CircuitConnection { get; init; }

    [Inject]
    public required IDataService DataService { get; init; }

    [Inject]
    public required IScreenshotService ScreenshotService { get; init; }

    [Inject]
    public required IToastService ToastService { get; init; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();

        await Register<ScreenshotReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleScreenshotReceivedMessage);

        _maxPerDevice = ScreenshotService.DefaultRetentionCount;

        await LoadDeviceSettings();
        await RefreshAsync();
    }

    public new void Dispose()
    {
        base.Dispose();
    }

    private string GetImageUrl(string fileName)
    {
        return $"api/screenshots/{DeviceId}/{fileName}?t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
    }

    private async Task HandleScreenshotReceivedMessage(object subscriber, ScreenshotReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            _latestFileName = message.FileName;
            _latestTimeStamp = message.TimeStamp;
            StateHasChanged();

            await LoadScreenshots();
        });
    }

    private async Task LoadDeviceSettings()
    {
        var device = await GetDevice();
        if (device is null)
        {
            return;
        }

        _directory = device.ScreenshotDirectory ?? ScreenshotService.GetScreenshotsDirectory(device);
        _retentionCount = device.ScreenshotRetentionCount > 0 ?
            device.ScreenshotRetentionCount :
            ScreenshotService.DefaultRetentionCount;
        _retentionHours = device.ScreenshotRetentionHours > 0 ?
            device.ScreenshotRetentionHours :
            ScreenshotService.DefaultRetentionHours;

        _pauseWindows.Clear();
        _pauseWindows.AddRange(ScreenshotPause.Deserialize(device.ScreenshotPauseWindows));
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

    private async Task<Device?> GetDevice()
    {
        var result = await DataService.GetDevice(DeviceId);
        return result.IsSuccess ? result.Value : null;
    }

    private async Task LoadScreenshots()
    {
        var screenshots = ScreenshotService.GetScreenshots(DeviceId, 100);

        _screenshots.Clear();
        _screenshots.AddRange(screenshots);

        if (_latestFileName is null && _screenshots.Count > 0)
        {
            _latestFileName = _screenshots[0].FileName;
            _latestTimeStamp = _screenshots[0].TimeStamp;
        }
    }

    private async Task RefreshAsync()
    {
        await LoadDeviceSettings();
        await LoadScreenshots();
        StateHasChanged();
    }

    private async Task StartMonitoring()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.StartMonitoring(DeviceId, _intervalMinutes);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = true;
            ToastService.ShowToast2($"Monitoring started for {DeviceName}.", ToastType.Success);
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
    /// Asks the device for a screenshot right away, without waiting for the
    /// monitoring interval.
    /// </summary>
    private async Task CaptureNow()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.CaptureScreenshotNow(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2($"A screenshot was requested from {DeviceName}.", ToastType.Info);
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
            var result = await CircuitConnection.StopMonitoring(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _isMonitoring = false;
            ToastService.ShowToast2($"Monitoring stopped for {DeviceName}.", ToastType.Info);
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

    private async Task SaveSettings()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.UpdateScreenshotSettings(
                DeviceId,
                _directory,
                _retentionCount,
                _retentionHours,
                _pauseWindows.Count == 0 ?
                    null :
                    ScreenshotPause.Serialize(_pauseWindows));

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            ToastService.ShowToast2("Screenshot settings saved.", ToastType.Success);
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

    private async Task DeleteScreenshot(Screenshot screenshot)
    {
        _isBusy = true;

        try
        {
            var result = await CircuitConnection.DeleteScreenshot(DeviceId, screenshot.FileName);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            if (_latestFileName == screenshot.FileName)
            {
                _latestFileName = null;
            }

            await LoadScreenshots();
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task ClearScreenshots()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.ClearScreenshots(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _latestFileName = null;
            _screenshots.Clear();

            ToastService.ShowToast2("All screenshots were deleted.", ToastType.Info);
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
}
