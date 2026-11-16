"""Splits the IDataService usage of the tests over the narrow interfaces."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TESTS = ROOT / "Tests" / "Server.Tests"

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
    server = ROOT / "Server" / "Services"
    for interface, file_name in INTERFACE_FILES.items():
        text = (server / file_name).read_text(encoding="utf-8")
        start = text.index(f"public interface {interface}")
        end = text.index("}", text.index(";", start))
        for match in MEMBER_RE.finditer(text[start:end]):
            mapping[match.group(1)] = interface
    return mapping


def field_of(interface):
    return "_" + interface[1].lower() + interface[2:]


def main():
    mapping = build_map()

    for path in sorted(TESTS.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        if "IDataService" not in text:
            continue

        # The members that are used through the injected object.
        used = set()
        for match in re.finditer(r"_dataService\.([A-Za-z0-9_]+)\s*\(", text):
            if match.group(1) in mapping:
                used.add(match.group(1))

        if not used:
            print(f"SKIP  {path.name}: no member call")
            continue

        interfaces = sorted({mapping[name] for name in used})
        print(f"FILE  {path.name}: {', '.join(interfaces)}")

        # The call sites.
        for match in reversed(list(re.finditer(r"_dataService\.([A-Za-z0-9_]+)\s*\(", text))):
            owner = mapping.get(match.group(1))
            if owner is None:
                continue
            text = (text[:match.start()] + field_of(owner) +
                    text[match.start() + len("_dataService"):])

        # The field declaration.
        declaration = re.search(
            r"^([ \t]*)private\s+IDataService\s+_dataService[^;]*;",
            text,
            re.MULTILINE)
        if declaration:
            indent = declaration.group(1)
            fields = ("\n" + indent).join(
                f"private {i} {field_of(i)} = null!;" for i in interfaces)
            text = text[:declaration.start()] + indent + fields + text[declaration.end():]

        # The initialization from the container.
        initialization = re.search(
            r"^([ \t]*)_dataService\s*=\s*IoCActivator\.ServiceProvider\.GetRequiredService<IDataService>\(\);",
            text,
            re.MULTILINE)
        if initialization:
            indent = initialization.group(1)
            lines = ("\n" + indent).join(
                f"{field_of(i)} = IoCActivator.ServiceProvider.GetRequiredService<{i}>();"
                for i in interfaces)
            text = (text[:initialization.start()] + indent + lines +
                    text[initialization.end():])

        path.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
