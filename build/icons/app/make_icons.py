#!/usr/bin/env python3
"""Pack the rendered PNGs (png/icon-<size>.png) into DevToolBox.ico (Windows) and DevToolBox.icns (macOS).

Usage: python3 -I make_icons.py   (run from build/icons/app; .icns needs macOS `iconutil`)
The PNGs themselves are rendered from devtoolbox-icon.svg's geometry; see README.md in this folder.
"""
import shutil
import struct
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PNG = HERE / "png"


def write_ico(sizes: list[int], target: Path) -> None:
    images = [(s, (PNG / f"icon-{s}.png").read_bytes()) for s in sizes]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, blobs = b"", b""
    for size, data in images:
        dim = 0 if size >= 256 else size  # 0 means 256 in the ICO directory
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)
    target.write_bytes(header + entries + blobs)


def write_icns(target: Path) -> None:
    if shutil.which("iconutil") is None:
        print("iconutil not found (macOS only); skipping .icns", file=sys.stderr)
        return
    iconset = HERE / "DevToolBox.iconset"
    iconset.mkdir(exist_ok=True)
    for base in (16, 32, 128, 256, 512):
        # macOS uses the padded, shadowed variant so it matches other dock icons.
        shutil.copy(PNG / f"icon-mac-{base}.png", iconset / f"icon_{base}x{base}.png")
        shutil.copy(PNG / f"icon-mac-{base * 2}.png", iconset / f"icon_{base}x{base}@2x.png")
    subprocess.run(["iconutil", "-c", "icns", str(iconset), "-o", str(target)], check=True)
    shutil.rmtree(iconset)


if __name__ == "__main__":
    write_ico([16, 24, 32, 48, 64, 128, 256], HERE / "DevToolBox.ico")
    write_icns(HERE / "DevToolBox.icns")
    print("wrote DevToolBox.ico and DevToolBox.icns")
