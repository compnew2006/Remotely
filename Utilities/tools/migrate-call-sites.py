"""Migrates the consumers of IDataService to the narrow domain interfaces.

Every consumer gets one injection per domain it actually uses.  The call
sites are rewritten to the injection that owns the member.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SERVER = ROOT / "Server"
TESTS = ROOT / "Tests"

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

RAZOR_INJECT = re.compile(r"@inject\s+IDataService\s+(\w+)")
PROPERTY_INJECT = re.compile(
    r"(public|private)\s+(required\s+)?IDataService\s+(\w+)\s*\{\s*get;\s*(set|init);\s*\}(\s*=\s*null!\s*;)?")
FIELD_INJECT = re.compile(r"private\s+readonly\s+IDataService\s+(\w+)\s*;")
PARAMETER_INJECT = re.compile(r"^([ \t]*)(.*?)IDataService\s+(\w+)\s*(,|\))", re.MULTILINE)


def build_map():
    mapping = {}
    for interface, file_name in INTERFACE_FILES.items():
        text = (SERVER / "Services" / file_name).read_text(encoding="utf-8")
        start = text.index(f"public interface {interface}")
        end = text.index("}", text.index(";", start))
        for match in MEMBER_RE.finditer(text[start:end]):
            name = match.group(1)
            if name in mapping and mapping[name] != interface:
                raise SystemExit(f"duplicate member {name}")
            mapping[name] = interface
    return mapping


def field_name(interface):
    return "_" + interface[1].lower() + interface[2:]


def property_name(interface):
    return interface[1].lower() + interface[2:]


def consumers():
    for root in (SERVER, TESTS):
        for path in sorted(root.rglob("*")):
            if path.suffix not in (".cs", ".razor"):
                continue
            if any(part in ("obj", "bin") for part in path.parts):
                continue
            if path.name in ("DataService.cs", "Program.cs", "IoCActivator.cs"):
                continue
            text = path.read_text(encoding="utf-8", errors="ignore")
            if "IDataService" not in text:
                continue
            yield path


def has_bare_usage(text, identifier):
    """True when the object is used without calling a member on it.

    The declarations and the assignments are masked out first, because they
    mention the name without calling anything on it.
    """
    masked = []
    for line in text.split("\n"):
        if "IDataService" in line or re.match(r"^\s*\w+\s*=\s*\w+\s*;\s*$", line):
            masked.append(" " * len(line))
        else:
            masked.append(line)

    pattern = re.compile(re.escape(identifier) + r"(?!\s*\.\s*[A-Za-z0-9_]+\s*\()")
    return bool(pattern.search("\n".join(masked)))

def migrate(path, mapping):
    text = path.read_text(encoding="utf-8")

    identifiers = set()
    for pattern in (RAZOR_INJECT, PROPERTY_INJECT, FIELD_INJECT):
        for match in pattern.finditer(text):
            identifiers.add(match.group(1) if pattern is not PROPERTY_INJECT else match.group(3))

    # A primary constructor keeps the name of the injected object.
    for match in PARAMETER_INJECT.finditer(text):
        if match.group(3).startswith("_"):
            identifiers.add(match.group(3))

    if not identifiers:
        return None

    used = set()
    for identifier in identifiers:
        if has_bare_usage(text, identifier):
            return ("skipped-bare", sorted(identifiers))
        for match in re.finditer(
                re.escape(identifier) + r"\s*\.\s*([A-Za-z0-9_]+)\s*\(", text):
            name = match.group(1)
            if name in mapping:
                used.add(name)

    if not used:
        return ("skipped-unused", sorted(identifiers))

    interfaces = sorted({mapping[name] for name in used})

    razor = RAZOR_INJECT.search(text)
    prop = PROPERTY_INJECT.search(text)
    field = FIELD_INJECT.search(text)

    if razor or prop:
        name_of = property_name
    else:
        name_of = field_name

    # 1. The call sites.
    for identifier in identifiers:
        matches = list(re.finditer(
            re.escape(identifier) + r"\s*\.\s*([A-Za-z0-9_]+)\s*\(", text))
        for match in reversed(matches):
            owner = mapping.get(match.group(1))
            if owner is None:
                continue
            text = (text[:match.start()] + name_of(owner) +
                    text[match.start() + len(identifier):])

    # 2. The razor injection.
    if razor:
        razor = RAZOR_INJECT.search(text)
        replacement = "\n".join(f"@inject {i} {property_name(i)}" for i in interfaces)
        text = text[:razor.start()] + replacement + text[razor.end():]
        path.write_text(text, encoding="utf-8")
        return ("migrated", interfaces)

    # 3. The injected property of a component code behind.
    if prop:
        visibility = prop.group(1)
        required = prop.group(2) or ""
        setter = "set" if "set;" in prop.group(0) else "init"
        initializer = prop.group(5) or ""
        line_start = text.rfind("\n", 0, prop.start()) + 1
        indent = text[line_start:prop.start()]
        before = text[:prop.start()]
        has_inject = before.rstrip().endswith("[Inject]")
        if has_inject:
            # The attribute is added to every property, so the original one
            # above the first property is removed.
            before = re.sub(r"[ \t]*\[Inject\][ \t]*\r?\n[ \t]*$", "", before)
        blocks = []
        for i in interfaces:
            prefix = f"{indent}[Inject]\n{indent}" if has_inject else ""
            blocks.append(
                prefix +
                f"{visibility} {required}{i} {property_name(i)} {{ get; {setter}; }}{initializer}")
        text = before + "\n".join(blocks) + text[prop.end():]

        # The markup of the component uses the same property.
        markup = path.with_suffix(".razor")
        if markup.exists():
            markup_text = markup.read_text(encoding="utf-8")
            for match in reversed(list(re.finditer(
                    r"\bDataService\.([A-Za-z0-9_]+)\s*\(", markup_text))):
                owner = mapping.get(match.group(1))
                if owner is None:
                    continue
                markup_text = (markup_text[:match.start()] + property_name(owner) +
                               markup_text[match.start() + len("DataService"):])
            markup.write_text(markup_text, encoding="utf-8")

    # 4. The field of a class.
    old_field = field.group(1) if field else None
    if field:
        declarations = "\n    ".join(
            f"private readonly {i} {field_name(i)};" for i in interfaces)
        text = text[:field.start()] + declarations + text[field.end():]

    # 5. The constructor parameter.
    parameter = PARAMETER_INJECT.search(text)
    if parameter:
        indent = parameter.group(1)
        before = parameter.group(2)
        # A primary constructor keeps the field names, a normal constructor
        # uses plain parameter names.
        if parameter.group(3).startswith("_"):
            names = [field_name(i) for i in interfaces]
        else:
            names = [field_name(i)[1:] for i in interfaces]
        params = (",\n" + indent).join(f"{i} {n}" for i, n in zip(interfaces, names))
        text = (text[:parameter.start()] + indent + before + params +
                parameter.group(4) + text[parameter.end():])

        # 6. The assignment in the constructor body.
        if old_field:
            assignment = re.search(
                r"^(\s*)" + re.escape(old_field) + r"\s*=\s*\w+\s*;",
                text,
                re.MULTILINE)
            if assignment:
                indent = assignment.group(1)
                assignments = "\n".join(
                    f"{indent}{field_name(i)} = {field_name(i)[1:]};" for i in interfaces)
                text = text[:assignment.start()] + assignments + text[assignment.end():]

    path.write_text(text, encoding="utf-8")
    # The replacement can leave two blank lines behind.
    text = re.sub(r"\n{3,}", "\n\n", path.read_text(encoding="utf-8"))
    path.write_text(text, encoding="utf-8")
    return ("migrated", interfaces)


def main():
    mapping = build_map()
    report = []
    for path in consumers():
        result = migrate(path, mapping)
        if result:
            report.append((path.relative_to(ROOT), result))

    for path, (status, detail) in report:
        if status == "migrated":
            print(f"OK       {path}: {', '.join(detail)}")
        else:
            print(f"SKIPPED  {path}: {status} ({', '.join(detail)})")

    print()
    print(f"migrated: {sum(1 for _, (s, _) in report if s == 'migrated')}")
    print(f"skipped : {sum(1 for _, (s, _) in report if s != 'migrated')}")


if __name__ == "__main__":
    main()
