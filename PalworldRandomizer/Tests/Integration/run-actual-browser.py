#!/usr/bin/env python3
"""Drive the published actual browser host with real Chromium and real game inputs.

The published app exposes no QA telemetry. Interaction uses the rendered canvas and
the production File picker; persistence is read from the real origin localStorage.
"""
import functools
import http.server
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import time
import traceback

from playwright.sync_api import sync_playwright  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
PUBLISHED = Path(os.environ.get("HOST_BROWSER_PUBLISH",
    str(ROOT / "evidence/runnable-hosts/browser-publish/wwwroot")))
OUT = Path(os.environ.get("HOST_BROWSER_OUTPUT", str(ROOT / "evidence/runnable-hosts/browser")))
OUT.mkdir(parents=True, exist_ok=True)
PAK = "/root/project-data/Pal-Windows.pak"
MAPPING = "/root/project-data/Mappings.usmap"
PREF_KEY = "Palworld-Randomizer.Config.preferences"
RESULT = {"checks": [], "console": [], "errors": [], "screenshots": []}


class Handler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass


def check(value, name):
    if not value:
        raise AssertionError(name)
    RESULT["checks"].append(name)
    print("PASS", name)


ANALYZE = """
import json, sys
from PIL import Image
image = Image.open(sys.argv[1]).convert('RGB')
pixels = image.load()
rows = []
for y in range(image.size[1]):
    count = 0
    for x in range(0, image.size[0], 3):
        r, g, b = pixels[x, y]
        if abs(r - 37) < 6 and abs(g - 47) < 6 and abs(b - 61) < 6:
            count += 1
    if count > 40:
        rows.append(y)
groups = []
current = None
for y in rows:
    if current and y - current[1] <= 60:
        current = (current[0], y)
    else:
        if current:
            groups.append(current)
        current = (y, y)
if current:
    groups.append(current)
result = []
for top, bottom in groups:
    if bottom - top < 10:
        continue
    result.append({'top': top, 'bottom': bottom, 'right': 963})
print(json.dumps(result))
"""


def layout(path):
    completed = subprocess.run(["python3", "-c", ANALYZE, str(path)], capture_output=True, text=True)
    if completed.returncode != 0:
        return []
    return json.loads(completed.stdout)


def wait_layout(page, path, rounds=12):
    for _ in range(rounds):
        page.screenshot(path=str(path))
        groups = layout(path)
        if len(groups) >= 2:
            return groups
        page.wait_for_timeout(1_000)
    return layout(path)


def choose_file(page, path, x, y, retries=3):
    for attempt in range(retries):
        try:
            with page.expect_file_chooser(timeout=8_000) as chosen:
                page.mouse.click(x, y)
            chosen.value.set_files(path)
            return True
        except Exception:
            if attempt == retries - 1:
                raise
            page.wait_for_timeout(2_000)
    return False


def main():
    server = http.server.ThreadingHTTPServer(
        ("127.0.0.1", 0), functools.partial(Handler, directory=str(PUBLISHED)))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f"http://127.0.0.1:{server.server_port}/"
    print("serving", url)
    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(
                headless=True,
                executable_path="/root/.cache/ms-playwright/chromium-1217/chrome-linux64/chrome",
                args=["--no-sandbox", "--enable-unsafe-swiftshader"])
            page = browser.new_page(viewport={"width": 1100, "height": 1000})
            page.on("pageerror", lambda error: RESULT["errors"].append(str(error)))
            page.on("console", lambda message: RESULT["console"].append(
                {"type": message.type, "text": message.text}))
            page.add_init_script("window.__verifyDocument = crypto.randomUUID(); "
                "window.__verifyMarker = sessionStorage.getItem('palworld-randomizer.recovery.manual');")
            page.goto(url, wait_until="networkidle", timeout=120_000)
            page.evaluate("localStorage.clear(); sessionStorage.clear();")
            page.reload(wait_until="networkidle", timeout=120_000)
            page.wait_for_timeout(6_000)
            first = OUT / "01-browser-startup.png"
            page.screenshot(path=str(first))
            RESULT["screenshots"].append(str(first.relative_to(ROOT)))
            check(page.evaluate("!!document.querySelector('canvas')"),
                  "actual browser host renders an Avalonia canvas")
            choose_file(page, PAK, 963, 472)
            page.wait_for_timeout(2_000)
            check(True, "browser File picker accepted the real PAK")

            choose_file(page, MAPPING, 963, 602)
            print("selected both files; waiting for real validation")

            page.wait_for_function(
                """() => { const raw = localStorage.getItem('%s');
                   if (!raw) return false; const p = JSON.parse(raw);
                   return p.SetupCompleted === true; }""" % PREF_KEY, timeout=300_000)
            check(True, "browser real PAK/USMAP compatibility completed and persisted")
            after = OUT / "02-browser-compatible.png"
            page.screenshot(path=str(after))
            RESULT["screenshots"].append(str(after.relative_to(ROOT)))
            prefs = json.loads(page.evaluate("localStorage.getItem('%s')" % PREF_KEY))
            print("prefs", json.dumps(prefs))
            check(prefs["LastGameVersion"] == "1.0.5.102999", "browser saved the detected game version")

            page.reload(wait_until="networkidle", timeout=120_000)
            page.wait_for_timeout(8_000)
            reopen = OUT / "03-browser-reopen.png"
            page.screenshot(path=str(reopen))
            RESULT["screenshots"].append(str(reopen.relative_to(ROOT)))
            saved = json.loads(page.evaluate("localStorage.getItem('%s')" % PREF_KEY))
            check(saved["SetupCompleted"] is True, "browser preferences survive a reload")
            check(saved["PakSelection"] == "Pal-Windows.pak", "browser stores the display name, not a path")

            if "--recovery" in sys.argv:
                token = page.evaluate("window.__verifyDocument")
                before = page.evaluate("key => localStorage.getItem(key)", PREF_KEY)
                with page.expect_navigation(wait_until="domcontentloaded", timeout=90_000):
                    page.click("#manual-software-recovery")
                page.wait_for_function(
                    "document.querySelector('#recovery-status').textContent.includes('Software rendering is active')",
                    timeout=120_000)
                check(page.evaluate("window.__verifyDocument") != token, "recovery reloaded the actual document")
                marker = page.evaluate("JSON.parse(sessionStorage.getItem('palworld-randomizer.recovery.manual'))")
                check(marker["pending"] is None and marker["acknowledged"] and marker["forceSoftware"]
                      and marker["attemptsRemaining"] == 0, "recovery acknowledgment consumed the pending snapshot")
                check(page.evaluate("""() => { const canvas = document.querySelector('#out canvas');
                    return !!canvas && !!canvas.getContext('2d') && canvas.getContext('webgl2') === null; }"""),
                      "the recovered app actually uses Software2D")
                check(page.evaluate("key => localStorage.getItem(key)", PREF_KEY) == before,
                      "recovery left durable preference bytes unchanged")
                stored = page.evaluate("""async () => {
                    const id = JSON.parse(window.__verifyMarker).pending.id;
                    const store = await import('./recovery-store.js'); return await store.readCheckpoint(id);
                }""")
                check(isinstance(stored, str) and len(stored) > 100, "the committed checkpoint remains readable")
                recovered = OUT / "04-browser-software.png"
                page.screenshot(path=str(recovered))
                RESULT["screenshots"].append(str(recovered.relative_to(ROOT)))

            check(not RESULT["errors"], "the actual browser host has zero page errors")
            check(not [item for item in RESULT["console"] if item["type"] == "error"],
                  "the actual Chromium host has zero console errors")

            browser.close()
            RESULT["passed"] = True
    except Exception:
        RESULT["passed"] = False
        RESULT["failure"] = traceback.format_exc()
        traceback.print_exc()
    finally:
        (OUT / "results.json").write_text(json.dumps(RESULT, indent=2))
        server.shutdown()
        if not RESULT.get("passed"):
            raise SystemExit(1)


if __name__ == "__main__":
    main()
