#!/usr/bin/env python3
"""Launch the actual Desktop host on owned Xvfb and capture first-frame evidence.

This drives the production application (no QA fixture host, no telemetry file) with
raw X11/XTest events and x11grab screenshots. The optional action argument types the
real PAK/USMAP paths into the Setup fields and waits for validation.
"""
import ctypes as C
import ctypes.util
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(os.environ.get("HOST_OUTPUT", str(ROOT / "evidence/runnable-hosts/desktop")))
OUT.mkdir(parents=True, exist_ok=True)
DATA = Path(os.environ.get("HOST_DATA_ROOT", str(OUT / ("metadata-" + uuid.uuid4().hex))))
DATA.mkdir(parents=True, exist_ok=True)
APP = Path(os.environ.get("APP_DLL", str(ROOT / ".build/PalworldRandomizer.Desktop/Debug/PalworldRandomizer.Desktop.dll")))
PAK = "/root/project-data/Pal-Windows.pak"
MAPPING = "/root/project-data/Mappings.usmap"

x11 = C.CDLL(ctypes.util.find_library("X11"))
xtst = C.CDLL(ctypes.util.find_library("Xtst"))
x11.XOpenDisplay.argtypes = [C.c_char_p]
x11.XOpenDisplay.restype = C.c_void_p
x11.XDefaultRootWindow.argtypes = [C.c_void_p]
x11.XDefaultRootWindow.restype = C.c_ulong
x11.XQueryPointer.argtypes = [C.c_void_p, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_ulong),
    C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_uint)]
x11.XSetInputFocus.argtypes = [C.c_void_p, C.c_ulong, C.c_int, C.c_ulong]
x11.XFlush.argtypes = [C.c_void_p]
x11.XCloseDisplay.argtypes = [C.c_void_p]
x11.XStringToKeysym.argtypes = [C.c_char_p]
x11.XStringToKeysym.restype = C.c_ulong
x11.XKeysymToKeycode.argtypes = [C.c_void_p, C.c_ulong]
xtst.XTestFakeMotionEvent.argtypes = [C.c_void_p, C.c_int, C.c_int, C.c_int, C.c_ulong]
xtst.XTestFakeButtonEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]
xtst.XTestFakeKeyEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]

display_name = ":" + str(next(i for i in range(240, 260) if not Path(f"/tmp/.X11-unix/X{i}").exists()))
display = None
xvfb = app = None


def key(name, down=None):
    code = x11.XKeysymToKeycode(display, x11.XStringToKeysym(name.encode()))
    if down is None:
        xtst.XTestFakeKeyEvent(display, code, True, 0)
        xtst.XTestFakeKeyEvent(display, code, False, 0)
    else:
        xtst.XTestFakeKeyEvent(display, code, down, 0)
    x11.XFlush(display)


def click(x, y):
    xtst.XTestFakeMotionEvent(display, -1, round(x), round(y), 0)
    x11.XFlush(display)
    root_return, child_return = C.c_ulong(), C.c_ulong()
    coords = [C.c_int() for _ in range(4)]
    mask = C.c_uint()
    x11.XQueryPointer(display, x11.XDefaultRootWindow(display), C.byref(root_return), C.byref(child_return),
                      *[C.byref(value) for value in coords], C.byref(mask))
    if child_return.value:
        x11.XSetInputFocus(display, child_return.value, 2, 0)
        x11.XFlush(display)
    xtst.XTestFakeButtonEvent(display, 1, True, 0)
    xtst.XTestFakeButtonEvent(display, 1, False, 0)
    x11.XFlush(display)


def type_text(value):
    for char in value:
        if char.isupper():
            key("Shift_L", True)
        key({"/": "slash", ".": "period", "-": "minus", "_": "underscore"}.get(char, char.lower()))
        if char.isupper():
            key("Shift_L", False)


def shot(name):
    path = OUT / f"{name}.png"
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-f", "x11grab", "-video_size", "1500x1200",
                    "-i", display_name, "-frames:v", "1", str(path)], check=True)
    print("screenshot", path)
    return path


def rss_mb(pid):
    try:
        for line in Path(f"/proc/{pid}/status").read_text().splitlines():
            if line.startswith("VmHWM:"):
                return int(line.split()[1]) / 1024
    except FileNotFoundError:
        pass
    return None


def main():
    global display, xvfb, app
    try:
        xvfb = subprocess.Popen(["Xvfb", display_name, "-screen", "0", "1500x1200x24", "-nolisten", "tcp"],
                                stdout=(OUT / "xvfb.log").open("w"), stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            display = x11.XOpenDisplay(display_name.encode())
            if display:
                break
            time.sleep(0.05)
        if not display:
            raise RuntimeError("Xvfb display did not open")

        app = subprocess.Popen(["/root/.dotnet/dotnet", str(APP)],
                               env=dict(os.environ, DISPLAY=display_name, XDG_DATA_HOME=str(DATA)),
                               stdout=(OUT / "host.log").open("w"), stderr=subprocess.STDOUT)
        time.sleep(6)
        shot("01-startup")
        if app.poll() is not None:
            raise RuntimeError("Desktop process exited before its first-frame check; see host.log")
        print("pid", app.pid, "rss_mb", rss_mb(app.pid))

        if "--type-paths" in sys.argv:
            # Coordinates measured from the captured window: content spans x~435-1215.
            click(700, 602)
            time.sleep(0.4)
            key("Control_L", True)
            key("a")
            key("Control_L", False)
            type_text(PAK)
            key("Return")
            time.sleep(2)
            click(700, 732)
            time.sleep(0.4)
            key("Control_L", True)
            key("a")
            key("Control_L", False)
            type_text(MAPPING)
            key("Return")
            print("typed both paths; waiting for validation")
            prefs = DATA / "Palworld-Randomizer/Config/preferences.json"
            peak = 0.0
            deadline = time.monotonic() + 180
            done = False
            while time.monotonic() < deadline:
                r = rss_mb(app.pid)
                if r:
                    peak = max(peak, r)
                try:
                    if "\"SetupCompleted\":true" in prefs.read_text():
                        done = True
                        break
                except FileNotFoundError:
                    pass
                time.sleep(1)
            time.sleep(1)
            shot("02-after-validate")
            print("validated", done, "peak_rss_mb", round(peak, 1), "exit", app.poll())
            if not done or app.poll() is not None:
                raise RuntimeError("The actual desktop compatibility check did not complete")
            saved = json.loads(prefs.read_text())
            if saved["PakSelection"] != PAK or saved["MappingSelection"] != MAPPING:
                raise RuntimeError("Compatibility did not persist the selected real input paths")
            print("prefs", prefs.read_text() if prefs.exists() else "MISSING")
            (OUT / "preferences-verified.json").write_text(json.dumps(saved, indent=2))
        print("done")
    finally:
        if app and app.poll() is None:
            app.terminate()
            app.wait(timeout=10)
        if display:
            x11.XCloseDisplay(display)
        if xvfb:
            xvfb.terminate()
            xvfb.wait(timeout=10)


if __name__ == "__main__":
    main()
