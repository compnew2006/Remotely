using Remotely.Desktop.Shared.Abstractions;
using Microsoft.Extensions.Logging;
using Remotely.Shared.Models;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Remotely.Desktop.Shared.Services;

public interface IChatHostService
{
    Task StartChat(string requesterID, string organizationName);
}
public class ChatHostService : IChatHostService
{
    private readonly IChatUiService _chatUiService;
    private readonly ILogger<ChatHostService> _logger;

    private NamedPipeServerStream? _namedPipeStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public ChatHostService(IChatUiService chatUiService, ILogger<ChatHostService> logger)
    {
        _chatUiService = chatUiService;
        _logger = logger;
    }

    public async Task StartChat(string pipeName, string organizationName)
    {
        // G5 (authenticated local IPC, least privilege): the pipe is created
        // with an explicit ACL granting read/write only to SYSTEM and the
        // current interactive user. The default pipe ACL would allow broader
        // same-session access; the GUID pipe name alone is not a boundary.
        // Mutual client authentication remains a SPEC-002 (OD-2) concern; this
        // closes the unauthenticated-listener gap.
        NamedPipeServerStream pipeStream;
        if (OperatingSystem.IsWindows())
        {
            var pipeSecurity = CreatePipeSecurity();
            pipeStream = NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                10,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity);
        }
        else
        {
            // Non-Windows: named pipes live under the user's socket directory
            // and inherit its permissions; no Windows ACL applies.
            pipeStream = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        _namedPipeStream = pipeStream;
        _writer = new StreamWriter(_namedPipeStream);
        _reader = new StreamReader(_namedPipeStream);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            _logger.LogInformation("Waiting for chat client to connect via pipe {name}.", pipeName);
            await _namedPipeStream.WaitForConnectionAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("A chat session was attempted, but the client failed to connect in time.");
            Environment.Exit(0);
        }

        _logger.LogInformation("Chat client connected.");
        _chatUiService.ChatWindowClosed += OnChatWindowClosed;

        _chatUiService.ShowChatWindow(organizationName, _writer);

        _ = Task.Run(ReadFromStream);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();

        // SYSTEM (the agent service and this process both run as SYSTEM).
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        security.AddAccessRule(new PipeAccessRule(
            systemSid,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // The current interactive user (the chat UI context).
        var currentUser = new NTAccount(Environment.UserDomainName, Environment.UserName);
        security.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        security.SetOwner(systemSid);
        return security;
    }

    private void OnChatWindowClosed(object? sender, EventArgs e)
    {
        try
        {
            _namedPipeStream?.Dispose();
        }
        catch { }
    }

    private async Task ReadFromStream()
    {
        while (_namedPipeStream?.IsConnected == true)
        {
            try
            {
                var messageJson = await _reader!.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(messageJson))
                {
                    var chatMessage = JsonSerializer.Deserialize<ChatMessage>(messageJson);
                    if (chatMessage is null)
                    {
                        _logger.LogWarning("Deserialized message was null.  Value: {value}", messageJson);
                        continue;
                    }
                    await _chatUiService.ReceiveChat(chatMessage);

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading from chat IPC stream.");
            }
        }
    }
}
