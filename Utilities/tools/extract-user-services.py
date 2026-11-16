"""Moves the user, invite, and device group methods out of DataService.cs.

The bodies are written to three temporary files that the assembly script
merges into the domain services.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Server" / "Services" / "DataService.cs"
OUT = ROOT / "Utilities" / "tools" / "_extracted"

GROUPS = {
    "UserService": [
        "CreateUser", "DeleteUser", "ChangeUserIsAdmin", "GetUserByName",
        "GetAllUsersInOrganization", "GetAllUsersForServer", "DoesUserExist",
        "DoesUserHaveAccessToDevice", "FilterDeviceIdsByUserPermission",
        "FilterUsersByDevicePermission", "UpdateUserOptions",
        "SetIsServerAdmin", "GetServerAdmins",
    ],
    "InviteService": [
        "AddInvite", "DeleteInvite", "JoinViaInvitation", "GetAllInviteLinks",
    ],
    "DeviceGroupService": [
        "AddDeviceGroup", "DeleteDeviceGroup", "AddDeviceToGroup",
        "GetDeviceGroups", "GetDeviceGroupsForOrganization",
    ],
}


def span(lines, index):
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
    OUT.mkdir(parents=True, exist_ok=True)

    groups = {}
    spans = []
    for group, names in GROUPS.items():
        blocks = []
        for name in names:
            pattern = re.compile(r"^    (public|private|internal|protected)\b.*\b" + re.escape(name) + r"\s*\(")
            for index, line in enumerate(lines):
                if pattern.match(line):
                    start, end = span(lines, index)
                    spans.append((start, end))
                    blocks.append("\n".join(lines[start:end + 1]))
                    break
            else:
                raise RuntimeError("method not found: " + name)
        groups[group] = blocks

    for start, end in sorted(spans, reverse=True):
        while end + 1 < len(lines) and lines[end + 1].strip() == "":
            end += 1
        del lines[start:end + 1]

    # Remove the declarations from the interface.
    end_of_interface = next(i for i, line in enumerate(lines) if line.startswith("public class DataService"))
    all_names = [name for names in GROUPS.values() for name in names]
    out = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if i < end_of_interface and line.startswith("    ") and any(
            re.search(r"\b" + re.escape(name) + r"\s*\(", line) for name in all_names):
            while not lines[i].rstrip().endswith(";"):
                i += 1
            i += 1
            if i < len(lines) and lines[i].strip() == "":
                i += 1
            continue
        out.append(line)
        i += 1

    DATA.write_text("\n".join(out), encoding="utf-8")

    for group, blocks in groups.items():
        (OUT / (group + ".bodies.txt")).write_text("\n\n".join(blocks), encoding="utf-8")

    print("moved", len(spans), "method blocks into", len(groups), "services")


if __name__ == "__main__":
    main()
