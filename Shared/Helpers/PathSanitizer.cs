using System;
using System.IO;
using System.Linq;

namespace Remotely.Shared.Helpers;

/// <summary>
/// Sanitizes remote-supplied file names before they are combined with a local
/// directory (G3 — safe file transfer boundaries). The sanitized name can
/// never contain a directory component, so it can never escape the target
/// directory regardless of the platform's path syntax.
/// </summary>
public static class PathSanitizer
{
    /// <summary>
    /// Reduces an arbitrary remote file name to a bare file name.
    /// Returns false for empty names, names containing invalid characters,
    /// or names that reduce to nothing (e.g. path separators or dot entries).
    /// </summary>
    public static bool TrySanitizeFileName(string? fileName, out string sanitized)
    {
        sanitized = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        // Strip every directory component, including drive letters, rooted
        // paths, and relative traversal. This is the primary defence.
        var name = Path.GetFileName(fileName.Trim());

        if (string.IsNullOrWhiteSpace(name) ||
            name == "." ||
            name == "..")
        {
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        // Windows reserved device names are invalid as file names even with an
        // extension (e.g. "con.txt" opens the console device on legacy APIs).
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        if (nameWithoutExtension is "CON" or "PRN" or "AUX" or "NUL" ||
            System.Text.RegularExpressions.Regex.IsMatch(nameWithoutExtension, @"^COM[1-9]$") ||
            System.Text.RegularExpressions.Regex.IsMatch(nameWithoutExtension, @"^LPT[1-9]$"))
        {
            return false;
        }

        if (name.All(c => c == '.'))
        {
            return false;
        }

        sanitized = name;
        return true;
    }
}
