using System.Collections.Concurrent;
using Remotely.Shared.Models;

namespace Remotely.Server.Services;

/// <summary>
/// Remembers the cameras, screens and audio devices that a device reported
/// the last time it was asked to record. The reports themselves live on the
/// remote control sessions, which are removed when the desktop client
/// disconnects, so a manager that opens the camera window after a stop
/// would otherwise see no devices at all until the next enumeration.
/// </summary>
public interface ICameraDevicesStore
{
    void Store(string deviceId, CameraDeviceInfo[] cameras, CameraAudioDeviceInfo[] audioDevices);

    (CameraDeviceInfo[] Cameras, CameraAudioDeviceInfo[] AudioDevices) Get(string deviceId);
}

public class CameraDevicesStore : ICameraDevicesStore
{
    private readonly ConcurrentDictionary<string, (CameraDeviceInfo[] Cameras, CameraAudioDeviceInfo[] AudioDevices)> _devices = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string deviceId, CameraDeviceInfo[] cameras, CameraAudioDeviceInfo[] audioDevices)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        _devices[deviceId] = (cameras ?? Array.Empty<CameraDeviceInfo>(), audioDevices ?? Array.Empty<CameraAudioDeviceInfo>());
    }

    public (CameraDeviceInfo[] Cameras, CameraAudioDeviceInfo[] AudioDevices) Get(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) ||
            !_devices.TryGetValue(deviceId, out var report))
        {
            return (Array.Empty<CameraDeviceInfo>(), Array.Empty<CameraAudioDeviceInfo>());
        }

        return report;
    }
}
