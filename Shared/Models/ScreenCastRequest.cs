namespace Remotely.Shared.Models;

public class ScreenCastRequest
{
    public bool NotifyUser { get; set; }

    /// <summary>
    /// The server-owned access mode of the session this viewer belongs to
    /// (SPEC-001 / FR-017, FR-020). The device derives its control authority
    /// for this viewer from this value and from nothing else.
    /// </summary>
    public bool ViewOnly { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string ViewerId { get; set; } = string.Empty;
    public Guid StreamId { get; set; }
}
