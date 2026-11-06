using Remotely.Shared.Models;

namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that is showing the recording settings of a device
/// when the device reports its cameras and its audio devices.
/// </summary>
public record CameraDevicesReceivedMessage(
    string DeviceId,
    CameraDeviceInfo[] Cameras,
    CameraAudioDeviceInfo[] AudioDevices);
