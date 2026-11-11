#!/usr/bin/env bash
# Builds artifacts/installers/DevToolBox-<version>-linux-<arch>.AppImage on Linux.
#
#   build/installers/linux/build-appimage.sh            # this machine's architecture
#   build/installers/linux/build-appimage.sh arm64      # x64 | arm64 (Native AOT can't cross-compile: match the host)
#
# Publishes Native AOT, lays out an AppDir (AppRun, .desktop, icons) and packs it with appimagetool.
# Needs: .NET 10 SDK, `sudo apt install clang zlib1g-dev`, and appimagetool on PATH (or APPIMAGETOOL=/path/to/it):
#   https://github.com/AppImage/appimagetool/releases
# appimagetool fetches the AppImage runtime from GitHub unless you pass one: APPIMAGE_RUNTIME=/path/to/runtime-<arch>.
set -euo pipefail

repo="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$repo"

[[ "$(uname -s)" == Linux ]] || { echo "error: run this on Linux" >&2; exit 1; }

arch="${1:-}"
if [[ -z "$arch" ]]; then
  case "$(uname -m)" in
    x86_64) arch=x64 ;;
    aarch64 | arm64) arch=arm64 ;;
    *) echo "error: unsupported machine $(uname -m)" >&2; exit 1 ;;
  esac
fi
case "$arch" in
  x64) appimage_arch=x86_64 ;;
  arm64) appimage_arch=aarch64 ;;
  *) echo "error: arch must be x64 or arm64" >&2; exit 1 ;;
esac

appimagetool="${APPIMAGETOOL:-$(command -v appimagetool || command -v appimagetool-"$appimage_arch".AppImage || true)}"
[[ -n "$appimagetool" ]] || {
  echo "error: appimagetool not found; download it from https://github.com/AppImage/appimagetool/releases" >&2
  echo "       and put it on PATH or set APPIMAGETOOL=/path/to/appimagetool" >&2
  exit 1
}

rid="linux-$arch"
publish="artifacts/publish/$rid"
appdir="artifacts/installers/linux-work/DevToolBox.AppDir"
out=artifacts/installers
here=build/installers/linux

echo "==> Publishing $rid"
dotnet publish src/DevToolBox.App -c Release -r "$rid" -o "$publish"
version=$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"([0-9]+\.[0-9]+\.[0-9]+)".*/\1/p' version.json)

echo "==> Checking the published app"
if [[ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]] && command -v xvfb-run >/dev/null; then
  xvfb-run -a "$publish/DevToolBox" --smoke-exit | tee "$publish/smoke.log"
else
  "$publish/DevToolBox" --smoke-exit | tee "$publish/smoke.log"
fi
grep -q "tools_ok=" "$publish/smoke.log"

echo "==> Laying out the AppDir"
rm -rf "$(dirname "$appdir")"
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/256x256/apps" "$out"
# Debug symbols, XML docs and the smoke log are not shipped.
find "$publish" -maxdepth 1 -type f ! -name '*.pdb' ! -name '*.xml' ! -name '*.dbg' ! -name 'smoke.log' \
  -exec cp {} "$appdir/usr/bin/" \;
chmod +x "$appdir/usr/bin/DevToolBox"

install -m 755 "$here/AppRun" "$appdir/AppRun"
install -m 644 "$here/devtoolbox.desktop" "$appdir/devtoolbox.desktop"
install -m 644 "$here/devtoolbox.desktop" "$appdir/usr/share/applications/devtoolbox.desktop"
install -m 644 build/icons/app/png/icon-256.png "$appdir/devtoolbox.png"
install -m 644 build/icons/app/png/icon-256.png "$appdir/usr/share/icons/hicolor/256x256/apps/devtoolbox.png"
ln -s devtoolbox.png "$appdir/.DirIcon"

echo "==> Packing the AppImage"
image="$out/DevToolBox-$version-linux-$arch.AppImage"
runtime_args=()
[[ -n "${APPIMAGE_RUNTIME:-}" ]] && runtime_args=(--runtime-file "$APPIMAGE_RUNTIME")
# --appimage-extract-and-run lets appimagetool (itself an AppImage) run where FUSE is missing, e.g. containers.
ARCH="$appimage_arch" "$appimagetool" --appimage-extract-and-run "${runtime_args[@]}" "$appdir" "$image"
chmod +x "$image"

rm -rf "$(dirname "$appdir")"
echo "==> $image ($(du -h "$image" | cut -f1))"
