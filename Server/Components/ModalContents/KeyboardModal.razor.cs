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
/// Shows the text that is typed on a remote device while it is being typed,
/// and the text that was captured before, so it can be read and deleted.
/// </summary>
public partial class KeyboardModal : AuthComponentBase, IDisposable
{
    /// <summary>
    /// The number of logs that are shown at most.
    /// </summary>
    private const int MaxLogs = 200;

    private readonly List<KeyboardLog> _logs = new();
    private readonly List<ScreenshotPauseWindow> _pauseWindows = new();
    private string? _errorMessage;
    private int _retentionCount = 500;
    private int _retentionHours = 24;
    private bool _isEnabled;
    private bool _isBusy;
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
    public required IToastService ToastService { get; init; }

    [Inject]
    public required IJsInterop JsInterop { get; init; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();

        await Register<KeyboardLogReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleKeyboardLogReceivedMessage);

        await LoadDeviceSettings();
        LoadLogs();
    }

    public new void Dispose()
    {
        base.Dispose();
    }

    private async Task LoadDeviceSettings()
    {
        var deviceResult = await DataService.GetDevice(DeviceId);
        if (!deviceResult.IsSuccess)
        {
            return;
        }

        var device = deviceResult.Value;
        _isEnabled = device.KeyboardEnabled;
        _retentionCount = device.KeyboardRetentionCount > 0 ?
            device.KeyboardRetentionCount :
            500;
        _retentionHours = device.KeyboardRetentionHours > 0 ?
            device.KeyboardRetentionHours :
            24;

        _pauseWindows.Clear();
        _pauseWindows.AddRange(ScreenshotPause.Deserialize(device.KeyboardPauseWindows));
    }

    private void LoadLogs()
    {
        var logs = CircuitConnection.GetKeyboardLogs(DeviceId);

        _logs.Clear();
        _logs.AddRange(logs);
    }

    private async Task HandleKeyboardLogReceivedMessage(object subscriber, KeyboardLogReceivedMessage message)
    {
        if (!string.Equals(message.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _logs.Insert(0, new KeyboardLog
            {
                ID = Guid.NewGuid().ToString(),
                DeviceID = message.DeviceId,
                TimeStamp = message.TimeStamp,
                WindowTitle = message.WindowTitle,
                ProcessName = message.ProcessName,
                Text = message.Text,
                IsPasswordField = message.IsPasswordField
            });

            while (_logs.Count > MaxLogs)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }

            StateHasChanged();
        });
    }

    private async Task ToggleCapture(ChangeEventArgs args)
    {
        // The value of a checkbox is the string "on" when it is checked, so
        // it is read in both shapes.
        var enabled = args.Value switch
        {
            bool value => value,
            string value => string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.UpdateKeyboardSettings(
                DeviceId,
                enabled,
                _retentionCount,
                _retentionHours,
                _pauseWindows.Count == 0 ? null : ScreenshotPause.Serialize(_pauseWindows));

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
            }

            // The switch is read again from the device, because the setting
            // is kept even when the device is offline: the capture starts as
            // soon as the device comes online.
            await LoadDeviceSettings();

            if (!string.IsNullOrWhiteSpace(_errorMessage))
            {
                return;
            }

            ToastService.ShowToast2(
                enabled ?
                    $"The text that is typed on {DeviceName} is now captured." :
                    $"The capture of {DeviceName} was stopped.",
                enabled ? ToastType.Success : ToastType.Info);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            await LoadDeviceSettings();
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
            var result = await CircuitConnection.UpdateKeyboardSettings(
                DeviceId,
                _isEnabled,
                _retentionCount,
                _retentionHours,
                _pauseWindows.Count == 0 ? null : ScreenshotPause.Serialize(_pauseWindows));

            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            await LoadDeviceSettings();
            LoadLogs();
            ToastService.ShowToast2("Keyboard settings saved.", ToastType.Success);
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

    private async Task RefreshAsync()
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            await LoadDeviceSettings();
            LoadLogs();
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteLog(KeyboardLog log)
    {
        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.DeleteKeyboardLog(DeviceId, log.ID);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            LoadLogs();
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

    private async Task ClearLogs()
    {
        var confirmed = await JsInterop.Confirm(
            "Are you sure you want to delete every captured text of this device?");

        if (!confirmed)
        {
            return;
        }

        _isBusy = true;
        _errorMessage = null;

        try
        {
            var result = await CircuitConnection.ClearKeyboardLogs(DeviceId);
            if (!result.IsSuccess)
            {
                _errorMessage = result.Reason;
                return;
            }

            _logs.Clear();

            ToastService.ShowToast2(
                "All of the captured text was deleted.",
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
}
