#!/usr/bin/env bash
# Host integration gate: shared library plus the two specified runnable hosts.
set -u
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$root" || exit 2
fail=0
note() { printf '%s\n' "$*"; }

check() {
    local label="$1" path="$2" pattern="$3"
    if [ ! -f "$path" ]; then note "FAIL $label: missing $path"; fail=1; return; fi
    if ! grep -q -- "$pattern" "$path"; then note "FAIL $label: '$pattern' absent from $path"; fail=1; return; fi
    note "PASS $label"
}

check "shared is a net10.0 Avalonia library" PalworldRandomizer/PalworldRandomizer.csproj '<TargetFramework>net10.0</TargetFramework>'
if grep -q '<UseWPF>true</UseWPF>\|<UseWPF>True</UseWPF>' PalworldRandomizer/PalworldRandomizer.csproj; then
    note "FAIL shared still enables WPF"; fail=1
else
    note "PASS shared WPF surface excluded"
fi
check "desktop host project exists" PalworldRandomizer.Desktop/PalworldRandomizer.Desktop.csproj 'net10.0'
check "browser host project exists" PalworldRandomizer.Browser/PalworldRandomizer.Browser.csproj 'net10.0-browser'
check "solution lists desktop" PalworldRandomizer.slnx 'PalworldRandomizer.Desktop'
check "solution lists browser" PalworldRandomizer.slnx 'PalworldRandomizer.Browser'
check "shared App composition exists" PalworldRandomizer/App.axaml.cs 'class App'
check "shared shell view exists" PalworldRandomizer/Views/ShellView.axaml 'ShellView'
check "actual Setup view exists" PalworldRandomizer/Views/SettingsView.axaml 'SettingsView'
check "desktop validation source exists" PalworldRandomizer.Desktop/DesktopArchiveValidation.cs 'DesktopArchiveValidation'
check "browser preferences source exists" PalworldRandomizer.Browser/BrowserPreferences.cs 'BrowserPreferences'
check "browser recovery bridge exists" PalworldRandomizer.Browser/RecoveryHostBridge.cs 'RecoveryHostBridge'
check "browser index outside-canvas control" PalworldRandomizer.Browser/wwwroot/index.html 'recovery-panel'
check "browser index loads recovery script" PalworldRandomizer.Browser/wwwroot/index.html 'recovery.js'
check "browser index loads verified store" PalworldRandomizer.Browser/wwwroot/index.html 'recovery-store.js'
for f in Metadata/Locales/en-US.json Metadata/Locales/Setup.en-US.json Metadata/Locales/Recovery.en-US.json; do
    check "embedded locale $f" "PalworldRandomizer/$f" '"'
done
if [ "$fail" -ne 0 ]; then
    note "HOST INTEGRATION RED"
    exit 1
fi
note "HOST INTEGRATION GREEN"
