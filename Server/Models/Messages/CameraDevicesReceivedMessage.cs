using Remotely.Shared.Models;

namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that requested a camera recording when the device
/// reports the cameras that it has.
/// </summary>
public record CameraDevicesReceivedMessage(string DeviceId, CameraDeviceInfo[] Cameras);
