# Actual-host integration checks

Run from the repository root. These checks drive the production app, not a fixture composition.
Python GUI checks require Playwright (and its pinned Chromium installation) plus Pillow; desktop checks also
use Xvfb, X11/XTest and ffmpeg. Use a Python environment with those test dependencies installed.

```sh
dotnet build PalworldRandomizer.Desktop/PalworldRandomizer.Desktop.csproj -c Release
dotnet publish PalworldRandomizer.Browser/PalworldRandomizer.Browser.csproj -c Release \
    -o evidence/runnable-hosts/browser-publish
bash PalworldRandomizer/Tests/Integration/verify-hosts.sh
node PalworldRandomizer/Tests/Integration/recovery-fallback.cjs
python PalworldRandomizer/Tests/Integration/recovery-layout.py
APP_DLL="$PWD/.build/PalworldRandomizer.Desktop/Release/PalworldRandomizer.Desktop.dll" \
    python PalworldRandomizer/Tests/Integration/run-actual-desktop.py --type-paths
python PalworldRandomizer/Tests/Integration/run-actual-browser.py --recovery
```

The native harness defaults to a fresh metadata sandbox in its owned evidence output. `HOST_DATA_ROOT` can
select an explicit test root; it never defaults to the real user configuration. `APP_DLL` selects the built
native assembly (default Debug); `HOST_OUTPUT` selects screenshots/logs. Real read-only test inputs are
`/root/project-data/Pal-Windows.pak` and `/root/project-data/Mappings.usmap` in this development environment.

Native checks fail on process exit or unfinished validation. Browser checks require actual File selection,
compatibility persistence and, with `--recovery`, a new document, verified consumed marker, actual Software2D
canvas, unchanged preference bytes and retained checkpoint. Page or Chromium console errors fail the check.
The initial-markup geometry check is separate from renderer/startup certification.

`import-accepted.py` is a one-time migration/provenance helper, NOT a test or build step: rerunning it after
integration would replace integration edits with the earlier accepted component sources. Do not run it on
an integrated checkout. Generated evidence/publishes are ignored and must not be committed.

Firefox verification is deferred. Other OSes, native OS chooser automation, automatic GPU recovery and
unimplemented Randomizer/Generate/editor workflows are not certified by these checks.
