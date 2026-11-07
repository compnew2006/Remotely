using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Remotely.Server.Data;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Shared.Entities;
using Remotely.Shared.Helpers;
using Remotely.Shared.Interfaces;

namespace Remotely.Server.Services;

/// <summary>
/// Keeps the keyboard capture of the enabled devices running.  The capture
/// lives in the memory of the server, so it is restored when the server
/// starts again.
/// </summary>
public interface IKeyboardCaptureManager
{
    /// <summary>
    /// Starts the capture of a device, and returns true when it was started.
    /// </summary>
    Task<bool> StartAsync(
        string deviceId,
        string userConnectionId,
        string organizationId,
        string requesterName);

    /// <summary>
    /// Stops the capture of a device.
    /// </summary>
    Task StopAsync(string deviceId);

    /// <summary>
    /// Tells the device whether the text of a password field is captured as
    /// it is typed.
    /// </summary>
    Task SetShowPasswordsAsync(string deviceId, bool showPasswords);

    /// <summary>
    /// Starts the capture of every device that has it enabled.  It is called
    /// when the server starts.
    /// </summary>
    Task RestoreAsync();
}

public class KeyboardCaptureManager : IKeyboardCaptureManager
{
    private readonly IAgentHubSessionCache _agentSessionCache;
    private readonly IAppDbFactory _appDbFactory;
    private readonly IRemoteControlSessionCache _sessionCache;
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHubContext;
    private readonly IHubContext<DesktopHub, IDesktopHubClient> _desktopHubContext;
    private readonly ILogger<KeyboardCaptureManager> _logger;

    public KeyboardCaptureManager(
        IAgentHubSessionCache agentSessionCache,
        IAppDbFactory appDbFactory,
        IRemoteControlSessionCache sessionCache,
        IHubContext<AgentHub, IAgentHubClient> agentHubContext,
        IHubContext<DesktopHub, IDesktopHubClient> desktopHubContext,
        ILogger<KeyboardCaptureManager> logger)
    {
        _agentSessionCache = agentSessionCache;
        _appDbFactory = appDbFactory;
        _sessionCache = sessionCache;
        _agentHubContext = agentHubContext;
        _desktopHubContext = desktopHubContext;
        _logger = logger;
    }

    public async Task<bool> StartAsync(
        string deviceId,
        string userConnectionId,
        string organizationId,
        string requesterName)
    {
        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            _logger.LogWarning(
                "The keyboard capture of device {deviceId} was not started, because the device is not online.",
                deviceId);

            return false;
        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            return false;
        }

        await StopAsync(deviceId);

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            UserConnectionId = userConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            AccessMode = Shared.Enums.RemoteAccessMode.ViewOnly,
            OrganizationId = organizationId,
            RequireConsent = false,
            NotifyUserOnStart = false,
            IsKeyboardCapture = true
        };

        _sessionCache.AddOrUpdate($"{sessionId}", session);

        _logger.LogInformation(
            "The keyboard capture of device {deviceId} was started.",
            deviceId);

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            userConnectionId,
            requesterName,
            string.Empty,
            organizationId);

        return true;
    }

    public async Task StopAsync(string deviceId)
    {
        foreach (var session in _sessionCache.Sessions
            .Where(x => x.DeviceId == deviceId && x.IsKeyboardCapture))
        {
            if (!string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                try
                {
                    await _desktopHubContext.Clients
                        .Client(session.DesktopConnectionId)
                        .StopKeyboardLogging();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Error while stopping the keyboard capture on device {deviceId}.",
                        deviceId);
                }
            }

            _sessionCache.TryRemove($"{session.UnattendedSessionId}", out _);
        }
    }

    /// <summary>
    /// Tells the device whether the text of a password field is captured as
    /// it is typed.  The capture of a device that is not running is not
    /// changed, because the options are sent again when the capture starts.
    /// </summary>
    public async Task SetShowPasswordsAsync(string deviceId, bool showPasswords)
    {
        foreach (var session in _sessionCache.Sessions
            .Where(x => x.DeviceId == deviceId && x.IsKeyboardCapture))
        {
            if (string.IsNullOrWhiteSpace(session.DesktopConnectionId))
            {
                continue;
            }

            try
            {
                await _desktopHubContext.Clients
                    .Client(session.DesktopConnectionId)
                    .SetKeyboardCaptureOptions(showPasswords);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error while setting the keyboard capture options of device {deviceId}.",
                    deviceId);
            }
        }
    }

    public async Task RestoreAsync()
    {
        try
        {
            Device[] devices;

            using (var dbContext = _appDbFactory.GetContext())
            {
                devices = dbContext.Devices
                    .Where(x => x.KeyboardEnabled)
                    .ToArray();
            }

            foreach (var device in devices)
            {
                await StartAsync(
                    device.ID,
                    string.Empty,
                    device.OrganizationID,
                    "Remotely");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while restoring the keyboard captures.");
        }
    }
}

/// <summary>
/// Restores the keyboard captures of the enabled devices when the server
/// starts.
/// </summary>
public class KeyboardCaptureStartupService : IHostedService
{
    private readonly IKeyboardCaptureManager _manager;
    private readonly ILogger<KeyboardCaptureStartupService> _logger;

    public KeyboardCaptureStartupService(
        IKeyboardCaptureManager manager,
        ILogger<KeyboardCaptureStartupService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Restoring the keyboard captures of the enabled devices.");

        await _manager.RestoreAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
