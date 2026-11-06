namespace Remotely.Server.Models.Messages;

/// <summary>
/// Carries one chunk of live audio from a device to the circuit that is
/// listening to it.  The chunk is a complete, short WAV frame.
/// </summary>
public record AudioLiveChunkMessage(
    string DeviceId,
    byte[] Chunk);
