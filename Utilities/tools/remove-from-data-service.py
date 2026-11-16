"""Removes the moved method blocks from DataService.cs.

The bodies now live in the domain services; DataService keeps a delegating
stub for each of them.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Server" / "Services" / "DataService.cs"

NAMES = [
    "AddAlert",
    "DeleteAlert",
    "DeleteAllAlerts",
    "GetAlert",
    "GetAlerts",
    "CreateApiToken",
    "DeleteApiToken",
    "GetAllApiTokens",
    "GetApiKey",
    "RenameApiToken",
    "ValidateApiKey",
    "FilterUsersByDevicePermissionInternal",
]


def find_span(lines, index):
    start = index
    while start > 0 and lines[start - 1].strip().startswith("///"):
        start -= 1
    if "=>" in lines[index] and "{" not in lines[index]:
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
    raise RuntimeError("unbalanced braces")


def main():
    lines = DATA.read_text(encoding="utf-8").split("\n")

    spans = []
    for name in NAMES:
        pattern = re.compile(r"^    (public|private|internal|protected)\b.*\b" + re.escape(name) + r"\s*\(")
        for index, line in enumerate(lines):
            if pattern.match(line):
                spans.append(find_span(lines, index))

    for start, end in sorted(spans, reverse=True):
        while end + 1 < len(lines) and lines[end + 1].strip() == "":
            end += 1
        del lines[start:end + 1]

    DATA.write_text("\n".join(lines), encoding="utf-8")
    print(f"removed {len(spans)} method blocks")


if __name__ == "__main__":
    main()
