"""Analysis for the call site migration.

Builds a member -> interface map out of the narrow interfaces, then reports
which narrow interfaces every consumer of IDataService needs.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SERVER = ROOT / "Server"

INTERFACE_FILES = {
    "ISettingsService": "SettingsService.cs",
    "IDeviceService": "DeviceService.cs",
    "IApiTokenService": "ApiTokenService.cs",
    "IAlertService": "AlertService.cs",
    "IOrganizationService": "OrganizationService.cs",
    "IUserService": "UserService.cs",
    "IInviteService": "InviteService.cs",
    "IDeviceGroupService": "DeviceGroupService.cs",
    "ISavedScriptService": "SavedScriptService.cs",
    "IScriptScheduleService": "ScriptScheduleService.cs",
    "IScriptResultService": "ScriptResultService.cs",
    "ISharedFileService": "SharedFileService.cs",
    "IDeviceListingService": "DeviceListingService.cs",
    "IDeviceGroupMembershipService": "DeviceGroupMembershipService.cs",
    "IMaintenanceService": "MaintenanceService.cs",
}

MEMBER_RE = re.compile(r"^\s{4}(?:[A-Za-z0-9_<>\[\],\?\. ]+?)\s+([A-Za-z0-9_]+)\s*\(", re.MULTILINE)


def build_map():
    mapping = {}
    owners = {}
    for interface, file_name in INTERFACE_FILES.items():
        text = (SERVER / "Services" / file_name).read_text(encoding="utf-8")
        # only the interface part
        start = text.index(f"public interface {interface}")
        end = text.index("}", text.index(";", start))
        body = text[start:end]
        for match in MEMBER_RE.finditer(body):
            name = match.group(1)
            if name in mapping and mapping[name] != interface:
                raise SystemExit(f"duplicate member {name}: {mapping[name]} vs {interface}")
            mapping[name] = interface
            owners.setdefault(interface, []).append(name)
    return mapping, owners


def consumers():
    files = []
    for path in SERVER.rglob("*"):
        if path.suffix not in (".cs", ".razor"):
            continue
        if any(part in ("obj", "bin") for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        if "IDataService" not in text:
            continue
        if path.name == "DataService.cs":
            continue
        files.append(path)
    return files


def main():
    mapping, owners = build_map()
    print("members per interface:")
    for interface, names in owners.items():
        print(f"  {interface}: {len(names)}")

    print()
    for path in consumers():
        text = path.read_text(encoding="utf-8", errors="ignore")
        used = set()
        for match in re.finditer(r"(?:_dataService|DataService|_dataService)\.([A-Za-z0-9_]+)\s*\(", text):
            name = match.group(1)
            if name in mapping:
                used.add(name)
        interfaces = sorted({mapping[name] for name in used})
        unknown = sorted({name for name in
                          re.findall(r"\.([A-Za-z0-9_]+)\s*\(", text)
                          if False})
        print(f"{path.relative_to(ROOT)}")
        print(f"    interfaces: {', '.join(interfaces) if interfaces else '(none found)'}")
        print(f"    members   : {', '.join(sorted(used))}")


if __name__ == "__main__":
    main()
