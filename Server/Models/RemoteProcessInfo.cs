namespace Remotely.Server.Models;

/// <summary>
/// A single process running on a remote device, as reported by the device's
/// process listing command.
/// </summary>
public class RemoteProcessInfo
{
    public int Id { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public double MemoryMB { get; set; }
    public double CpuSeconds { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
}
