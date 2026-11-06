using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Remotely.Server.Enums;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Components.ModalContents;

/// <summary>
/// Shows the processes running on a remote device, similar to the Windows
/// Task Manager.  The list is retrieved with the device's own scripting shell,
/// so it works on any device that has an agent connected, and it can end a
/// process on that device.
/// </summary>
public partial class TaskManagerModal : AuthComponentBase, IDisposable
{
    /// <summary>
    /// Marks the process listing command so its result can be told apart from
    /// every other script result that arrives on the circuit.
    /// </summary>
    private const string ListCommandMarker = "#REMOTELY-TASKMANAGER-LIST";

    private const string KillCommandMarker = "#REMOTELY-TASKMANAGER-KILL";

    private const string ListCommand =
        "Get-Process -ErrorAction SilentlyContinue | " +
        "Select-Object Id,ProcessName,SessionId," +
        "@{N='MemoryMB';E={[math]::Round($_.WorkingSet64/1MB,1)}}," +
        "@{N='CPUSeconds';E={[math]::Round($_.CPU,1)}}," +
        "@{N='StartTime';E={try{$_.StartTime.ToString('yyyy-MM-dd HH:mm:ss')}catch{''}}}," +
        "@{N='WindowTitle';E={$_.MainWindowTitle}} | " +
        "Sort-Object -Property MemoryMB -Descending | ConvertTo-Json -Compress";

    private readonly List<RemoteProcessInfo> _processes = new();
    private System.Timers.Timer? _refreshTimer;
    private bool _autoRefresh = true;
    private string _filter = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;

    [Parameter]
    public string DeviceId { get; set; } = string.Empty;

    [Parameter]
    public string DeviceName { get; set; } = string.Empty;

    [Parameter]
    public string Platform { get; set; } = string.Empty;

    [Inject]
    public required ICircuitConnection CircuitConnection { get; init; }

    [Inject]
    public required IToastService ToastService { get; init; }

    private bool IsSupportedPlatform =>
        string.IsNullOrWhiteSpace(Platform) ||
        Platform.Equals("Windows", StringComparison.OrdinalIgnoreCase);

    private IEnumerable<RemoteProcessInfo> FilteredProcesses
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_filter))
            {
                return _processes;
            }

            return _processes.Where(x =>
                x.ProcessName.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                x.WindowTitle.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                x.Id.ToString() == _filter);
        }
    }

    /// <summary>
    /// Window titles are only reported for processes whose windows the
    /// requesting shell can see.  The agent's shell runs in session 0, so the
    /// column is hidden when the device does not report any.
    /// </summary>
    private bool HasWindowTitles =>
        _processes.Any(x => !string.IsNullOrWhiteSpace(x.WindowTitle));

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();

        await Register<ScriptResultMessage, string>(
            CircuitConnection.ConnectionId,
            HandleScriptResultMessage);

        _refreshTimer = new System.Timers.Timer(10000)
        {
            AutoReset = true
        };
        _refreshTimer.Elapsed += RefreshTimer_Elapsed;
        _refreshTimer.Start();

        await RefreshAsync();
    }

    public new void Dispose()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        base.Dispose();
    }

    private async Task RefreshAsync()
    {
        if (!IsSupportedPlatform)
        {
            _errorMessage = "The task manager is only available for Windows devices.";
            return;
        }

        _isLoading = true;
        _errorMessage = null;

        await CircuitConnection.ExecuteCommandOnAgent(
            ScriptingShell.WinPS,
            $"{ListCommand} {ListCommandMarker}",
            [DeviceId]);
    }

    private async Task EndTaskAsync(RemoteProcessInfo process)
    {
        var command =
            $"try {{ Stop-Process -Id {process.Id} -Force -ErrorAction Stop; " +
            $"'Process {process.Id} ({process.ProcessName}) was ended.' }} " +
            $"catch {{ 'Failed to end process {process.Id}: ' + $_.Exception.Message }} " +
            KillCommandMarker;

        await CircuitConnection.ExecuteCommandOnAgent(
            ScriptingShell.WinPS,
            command,
            [DeviceId]);
    }

    private async Task HandleScriptResultMessage(object subscriber, ScriptResultMessage message)
    {
        var result = message.ScriptResult;

        if (!string.Equals(result.DeviceID, DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var input = result.ScriptInput ?? string.Empty;

        if (input.Contains(KillCommandMarker, StringComparison.OrdinalIgnoreCase))
        {
            var output = (result.StandardOutput ?? Array.Empty<string>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("PS ", StringComparison.OrdinalIgnoreCase));

            await InvokeAsync(() =>
            {
                ToastService.ShowToast2(
                    string.IsNullOrWhiteSpace(output) ? "Process ended." : output,
                    ToastType.Info);
                StateHasChanged();
            });

            await RefreshAsync();
            return;
        }

        if (!input.Contains(ListCommandMarker, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var processes = ParseProcesses(result);

        await InvokeAsync(() =>
        {
            _processes.Clear();
            _processes.AddRange(processes);
            _isLoading = false;
            _errorMessage = processes.Count == 0 ?
                "The device did not return any processes." :
                null;
            StateHasChanged();
        });
    }

    private void RefreshTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_autoRefresh || _isLoading)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            try
            {
                await RefreshAsync();
            }
            catch
            {
                // The device may have gone offline between ticks.
            }
        });
    }

    private static List<RemoteProcessInfo> ParseProcesses(ScriptResult result)
    {
        var processes = new List<RemoteProcessInfo>();

        var lines = result.StandardOutput ?? Array.Empty<string>();
        var startIndex = Array.FindIndex(lines, line =>
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("[") || trimmed.StartsWith("{");
        });

        if (startIndex < 0)
        {
            return processes;
        }

        var json = string.Concat(lines.Skip(startIndex));

        try
        {
            var parsed = JsonSerializer.Deserialize<RemoteProcessInfo[]>(json, JsonSerializerHelper.CaseInsensitiveOptions);
            if (parsed is not null)
            {
                processes.AddRange(parsed);
                return processes;
            }
        }
        catch (JsonException)
        {
            // A single process is serialized as an object rather than an array.
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<RemoteProcessInfo>(json, JsonSerializerHelper.CaseInsensitiveOptions);
            if (parsed is not null)
            {
                processes.Add(parsed);
            }
        }
        catch (JsonException)
        {
            // The output was not in the expected format.
        }

        return processes;
    }
}
