using Remotely.Shared.Models;

namespace Remotely.Server.Models.Messages;

/// <summary>
/// Sent to the circuit that is showing the audio settings of a device when
/// the device reports its microphones.
/// </summary>
public record AudioDevicesReceivedMessage(
    string DeviceId,
    AudioDeviceInfo[] Devices);
