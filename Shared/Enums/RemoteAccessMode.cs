namespace Remotely.Shared.Enums;

/// <summary>
/// Server-owned access authority for a remote control session (SPEC-001 / FR-001).
/// Exactly two values are supported. The browser, the viewer, the link, and the
/// device MUST NOT be able to set, widen, or influence this value (FR-002).
/// </summary>
public enum RemoteAccessMode
{
    ViewOnly = 1,
    FullControl = 2
}
