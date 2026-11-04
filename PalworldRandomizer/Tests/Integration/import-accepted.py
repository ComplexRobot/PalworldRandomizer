#!/usr/bin/env python3
"""Copy accepted production sources byte-for-byte and record provenance hashes."""
import hashlib
import json
import pathlib
import shutil

ROOT = pathlib.Path(__file__).resolve().parents[3]
STAGING = ROOT.parents[1] / "staging"
DEST_SHARED = ROOT / "PalworldRandomizer"
DEST_DESKTOP = ROOT / "PalworldRandomizer.Desktop"
DEST_BROWSER = ROOT / "PalworldRandomizer.Browser"

COPIES = [
    ("005-shell-ui/PalworldRandomizer/Controls/ShellTabHeader.axaml", DEST_SHARED / "Controls"),
    ("005-shell-ui/PalworldRandomizer/Controls/ShellTabHeader.axaml.cs", DEST_SHARED / "Controls"),
    ("005-shell-ui/PalworldRandomizer/Controls/TabStripPanel.cs", DEST_SHARED / "Controls"),
    ("005-shell-ui/PalworldRandomizer/Localization/EnglishMetadata.cs", DEST_SHARED / "Localization"),
    ("005-shell-ui/PalworldRandomizer/Localization/LocaleKeyConverter.cs", DEST_SHARED / "Localization"),
    ("005-shell-ui/PalworldRandomizer/Metadata/Locales/en-US.json", DEST_SHARED / "Metadata/Locales"),
    ("005-shell-ui/PalworldRandomizer/ViewModels/LauncherViewModel.cs", DEST_SHARED / "ViewModels"),
    ("005-shell-ui/PalworldRandomizer/Views/LauncherView.axaml", DEST_SHARED / "Views"),
    ("005-shell-ui/PalworldRandomizer/Views/LauncherView.axaml.cs", DEST_SHARED / "Views"),
    ("005-shell-ui/PalworldRandomizer/Views/ShellView.axaml", DEST_SHARED / "Views"),
    ("005-shell-ui/PalworldRandomizer/Views/ShellView.axaml.cs", DEST_SHARED / "Views"),
    ("006-setup-ui/PalworldRandomizer/Localization/SetupText.cs", DEST_SHARED / "Localization"),
    ("006-setup-ui/PalworldRandomizer/Metadata/Locales/Setup.en-US.json", DEST_SHARED / "Metadata/Locales"),
    ("006-setup-ui/PalworldRandomizer/Models/AppPreferences.cs", DEST_SHARED / "Models"),
    ("006-setup-ui/PalworldRandomizer/Models/ArchiveCompatibility.cs", DEST_SHARED / "Models"),
    ("006-setup-ui/PalworldRandomizer/Serializer/AppPreferencesStore.cs", DEST_SHARED / "Serializer"),
    ("006-setup-ui/PalworldRandomizer/ViewModels/SetupViewModel.cs", DEST_SHARED / "ViewModels"),
    ("006-setup-ui/PalworldRandomizer/Views/SettingsView.axaml", DEST_SHARED / "Views"),
    ("006-setup-ui/PalworldRandomizer/Views/SettingsView.axaml.cs", DEST_SHARED / "Views"),
    ("006-setup-ui/PalworldRandomizer.Desktop/DesktopArchiveValidation.cs", DEST_DESKTOP),
    ("006-setup-ui/PalworldRandomizer.Browser/BrowserArchiveWorker.cs", DEST_BROWSER),
    ("006-setup-ui/PalworldRandomizer.Browser/BrowserFileStream.cs", DEST_BROWSER),
    ("006-setup-ui/PalworldRandomizer.Browser/BrowserPreferences.cs", DEST_BROWSER),
    ("006-setup-ui/PalworldRandomizer.Browser/BrowserSetupDrop.cs", DEST_BROWSER),
    ("006-setup-ui/PalworldRandomizer.Browser/BrowserSetupFiles.cs", DEST_BROWSER),
    ("007-managed-recovery/PalworldRandomizer.Browser/RecoveryState.cs", DEST_BROWSER),
    ("007-managed-recovery/PalworldRandomizer.Browser/WorkspaceRecovery.cs", DEST_BROWSER),
    ("008-recovery-store/PalworldRandomizer.Browser/wwwroot/recovery-store.js", DEST_BROWSER / "wwwroot"),
    ("009-manual-recovery/PalworldRandomizer.Browser/RecoveryHostBridge.cs", DEST_BROWSER),
    ("009-manual-recovery/PalworldRandomizer.Browser/wwwroot/recovery-controls.html", DEST_BROWSER / "wwwroot"),
    ("009-manual-recovery/PalworldRandomizer.Browser/wwwroot/recovery.js", DEST_BROWSER / "wwwroot"),
    ("009-manual-recovery/PalworldRandomizer/Metadata/Locales/Recovery.en-US.json", DEST_SHARED / "Metadata/Locales"),
]


def digest(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    manifest = {"sources": {}}
    for relative, destination in COPIES:
        source = STAGING / relative
        if not source.is_file():
            print("MISSING", source)
            return 1
        destination.mkdir(parents=True, exist_ok=True)
        target = destination / source.name
        shutil.copyfile(source, target)
        manifest["sources"][str(target.relative_to(ROOT))] = {
            "accepted": str(source.relative_to(STAGING)),
            "sha256": digest(target),
        }
    (ROOT / "evidence/runnable-hosts/source-import-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"imported {len(COPIES)} accepted production files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
