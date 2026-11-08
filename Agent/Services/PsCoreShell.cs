using Microsoft.Extensions.Logging;
using Remotely.Agent.Extensions;
using Remotely.Agent.Interfaces;
using Remotely.Shared.Dtos;
using Remotely.Shared.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

public interface IPsCoreShell : IDisposable, IScriptingShell
{
    string? SenderConnectionId { get; set; }

    PwshCommandCompletion GetCompletions(string inputText, int currentIndex, bool? forward);
    Task<ScriptResultDto> WriteInput(string input);
}

public class PsCoreShell : IPsCoreShell
{
    private readonly IConfigService _configService;
    private readonly ConnectionInfo _connectionInfo;
    private readonly ILogger<PsCoreShell> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly PowerShell _powershell;
    private int _disposedValue;
    private CommandCompletion? _lastCompletion;
    private string? _lastInputText;

    public PsCoreShell(
        IConfigService configService,
        ILogger<PsCoreShell> logger)
    {
        _configService = configService;
        _connectionInfo = _configService.GetConnectionInfo();
        _logger = logger;

        _powershell = PowerShell.Create();

        _powershell.AddScript($@"$VerbosePreference = ""Continue"";
                            $DebugPreference = ""Continue"";
                            $InformationPreference = ""Continue"";
                            $WarningPreference = ""Continue"";
                            $env:DeviceId = ""{_connectionInfo.DeviceID}"";
                            $env:ServerUrl = ""{_connectionInfo.Host}""");

        _powershell.Invoke();
    }

    public bool IsDisposed => Volatile.Read(ref _disposedValue) != 0;
    public bool IsBusy => _operationLock.CurrentCount == 0;
    public string? SenderConnectionId { get; set; }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public PwshCommandCompletion GetCompletions(string inputText, int currentIndex, bool? forward)
    {
        _operationLock.Wait();

        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PsCoreShell));
            }

            if (_lastCompletion is null ||
                inputText != _lastInputText)
            {
                _lastInputText = inputText;
                _lastCompletion = CommandCompletion.CompleteInput(inputText, currentIndex, new(), _powershell);
            }

            if (forward.HasValue)
            {
                _lastCompletion.GetNextResult(forward.Value);
            }

            return _lastCompletion.ToPwshCompletion();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<ScriptResultDto> WriteInput(string input)
    {
        var deviceId = _configService.GetConnectionInfo().DeviceID;
        await _operationLock.WaitAsync().ConfigureAwait(false);
        var sw = Stopwatch.StartNew();

        try
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PsCoreShell));
            }

            _powershell.Streams.ClearStreams();
            _powershell.Commands.Clear();

            _powershell.AddScript(input);
            var results = _powershell.Invoke();

            using var ps = PowerShell.Create();
            ps.AddScript("$args[0] | Out-String");
            ps.AddArgument(results);
            var result = await ps.InvokeAsync().ConfigureAwait(false);

            var hostOutput = result.Count > 0 ?
                $"{result[0].BaseObject}" :
                string.Empty;

            var verboseOut = _powershell.Streams.Verbose.ReadAll().Select(x => x.Message);
            var debugOut = _powershell.Streams.Debug.ReadAll().Select(x => x.Message);
            var errorOut = _powershell.Streams.Error.ReadAll().Select(x => x.Exception.ToString() + Environment.NewLine + x.ScriptStackTrace);
            var infoOut = _powershell.Streams.Information.Select(x => x.MessageData.ToString());
            var warningOut = _powershell.Streams.Warning.Select(x => x.Message);

            var standardOut = hostOutput.Split(Environment.NewLine)
                .Concat(infoOut)
                .Concat(debugOut)
                .Concat(verboseOut)
                .Select(x => $"{x}");

            var errorAndWarningOut = errorOut.Concat(warningOut).ToArray();

            return new ScriptResultDto()
            {
                DeviceID = _configService.GetConnectionInfo().DeviceID,
                SenderConnectionID = SenderConnectionId,
                ScriptInput = input,
                Shell = Shared.Enums.ScriptingShell.PSCore,
                StandardOutput = standardOut.ToArray(),
                ErrorOutput = errorAndWarningOut,
                RunTime = sw.Elapsed,
                HadErrors = _powershell.HadErrors || errorAndWarningOut.Any()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while writing input to PSCore.");
            return new ScriptResultDto()
            {
                DeviceID = deviceId,
                SenderConnectionID = SenderConnectionId,
                ScriptInput = input,
                Shell = Shared.Enums.ScriptingShell.PSCore,
                StandardOutput = Array.Empty<string>(),
                ErrorOutput = new[] { "Error while writing input." },
                RunTime = sw.Elapsed,
                HadErrors = true
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _operationLock.Wait();

        try
        {
            if (!IsDisposed)
            {
                _powershell.Dispose();
                Volatile.Write(ref _disposedValue, 1);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }
}
