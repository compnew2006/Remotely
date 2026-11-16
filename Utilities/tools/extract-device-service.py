"""One-off refactoring helper: move the device cluster out of DataService.

It cuts the selected method blocks (including their doc comments) out of
Server/Services/DataService.cs, writes them into Server/Services/DeviceService.cs
and leaves delegating stubs behind in DataService.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Server" / "Services" / "DataService.cs"
DEVICE = ROOT / "Server" / "Services" / "DeviceService.cs"

DEVICE_METHODS = [
    "AddOrUpdateDevice",
    "CreateDevice",
    "GetDevice",
    "GetDeviceCount",
    "GetTotalDevices",
    "UpdateDevice",
    "RemoveDevices",
    "SetAllDevicesNotOnline",
    "DeviceDisconnected",
    "SetServerVerificationToken",
    "UpdateTags",
    "UpdateScreenshotSettings",
    "UpdateCameraSettings",
    "UpdateAudioSettings",
    "UpdateKeyboardSettings",
]

HELPERS = ["FindExistingMediaDirectory", "GetPathComparer"]


def find_method_span(lines, name):
    """Returns (start, end) line indexes of the method, doc comment included."""
    pattern = re.compile(r"^    (public|private|internal|protected)\b.*\b" + re.escape(name) + r"\s*\(")
    for index, line in enumerate(lines):
        if pattern.match(line):
            start = index
            # include the doc comment block above the signature
            while start > 0 and lines[start - 1].strip().startswith("///"):
                start -= 1
            if "=>" in lines[index] and "{" not in lines[index]:
                # expression bodied member: it ends with the statement
                end = index
                while not lines[end].rstrip().endswith(";"):
                    end += 1
                return start, end
            depth = 0
            seen = False
            for end in range(index, len(lines)):
                for char in lines[end]:
                    if char == "{":
                        depth += 1
                        seen = True
                    elif char == "}":
                        depth -= 1
                if seen and depth == 0:
                    return start, end
            raise RuntimeError(f"unbalanced braces for {name}")
    raise RuntimeError(f"method not found: {name}")


def main():
    text = DATA.read_text(encoding="utf-8")
    lines = text.split("\n")

    moved = []
    spans = []
    for name in DEVICE_METHODS + HELPERS:
        occurrences = []
        for index, line in enumerate(lines):
            if re.match(r"^    (public|private|internal|protected)\b.*\b" + re.escape(name) + r"\s*\(", line):
                occurrences.append(index)
        if not occurrences:
            raise RuntimeError(f"method not found: {name}")
        for index in occurrences:
            start, end = find_method_span(lines, name)
            spans.append((start, end, name))

    # Merge overlapping spans.
    spans.sort()
    merged = []
    for start, end, name in spans:
        if merged and start <= merged[-1][1] + 1:
            merged[-1][1] = max(merged[-1][1], end)
            merged[-1][2].append(name)
        else:
            merged.append([start, end, [name]])

    for start, end, names in merged:
        moved.append((names, "\n".join(lines[start:end + 1])))

    # Remove the moved blocks (and the blank line that follows them).
    for start, end, _names in reversed(merged):
        while end + 1 < len(lines) and lines[end + 1].strip() == "":
            end += 1
        del lines[start:end + 1]

    DATA.write_text("\n".join(lines), encoding="utf-8")

    body = []
    for names, block in moved:
        body.append(block)
        body.append("")

    DEVICE.write_text(
        "\n".join(body),
        encoding="utf-8")

    print("moved blocks:")
    for names, _block in moved:
        print("  ", ", ".join(names))


if __name__ == "__main__":
    main()
