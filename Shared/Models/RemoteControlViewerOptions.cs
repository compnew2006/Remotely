using System.Runtime.Serialization;

namespace Remotely.Shared.Models;

[DataContract]
public class RemoteControlViewerOptions
{
    [DataMember]
    public bool ShouldRecordSession { get; init; }

    /// <summary>
    /// The server-owned access mode of the session the viewer is attached to
    /// (SPEC-001 / FR-002, FR-007). The viewer UI must not offer control
    /// affordances when this is true.
    /// </summary>
    [DataMember]
    public bool ViewOnly { get; init; }
}
