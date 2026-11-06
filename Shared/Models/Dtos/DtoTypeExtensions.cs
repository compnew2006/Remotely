using Remotely.Shared.Models.Dtos;

namespace Remotely.Shared.Models.Dtos;

/// <summary>
/// Single source of truth for which DTO types apply input or otherwise modify
/// the target device (SPEC-001 / FR-010, FR-017). Used by both the server relay
/// (server-side enforcement) and the device (defence in depth) so the two
/// enforcement points can never diverge.
/// </summary>
public static class DtoTypeExtensions
{
    /// <summary>
    /// DTO types that apply keyboard, pointer, or other device-modifying input.
    /// </summary>
    public static readonly IReadOnlySet<DtoType> ControlTypes =
        new HashSet<DtoType>
        {
            DtoType.MouseMove,
            DtoType.MouseDown,
            DtoType.MouseUp,
            DtoType.Tap,
            DtoType.MouseWheel,
            DtoType.KeyDown,
            DtoType.KeyUp,
            DtoType.CtrlAltDel,
            DtoType.ToggleBlockInput,
            DtoType.TextTransfer,
            DtoType.KeyPress,
            DtoType.SetKeyStatesUp,
            DtoType.File
        };

    /// <summary>
    /// Returns true when the DTO type applies input to or modifies the target
    /// device, and therefore requires FullControl authority.
    /// </summary>
    public static bool IsControlMessage(this DtoType dtoType)
    {
        return ControlTypes.Contains(dtoType);
    }
}
