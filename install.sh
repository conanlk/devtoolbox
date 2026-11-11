#!/usr/bin/env bash
# Installs the latest DevToolBox release on macOS or Linux.
#
#   curl -fsSL https://raw.githubusercontent.com/conanlk/devtoolbox/master/install.sh | bash
#
# macOS: downloads the universal .pkg and installs DevToolBox.app into /Applications (asks for your password).
# Linux: downloads the AppImage to ~/.local/share/devtoolbox, links ~/.local/bin/devtoolbox and adds a menu entry.
#
# The download is checked against the release's SHA256SUMS.txt. Files downloaded with curl don't get the macOS
# quarantine flag, so Gatekeeper doesn't block the unsigned package.
#
# Options (environment variables):
#   DEVTOOLBOX_VERSION=0.1.0     install that version instead of the latest release
#   DEVTOOLBOX_REPO=owner/name   download from another GitHub repository (forks)
#   DEVTOOLBOX_UNINSTALL=1       Linux: remove what this script installed
#
# Everything runs inside main(), so a partially downloaded script never runs half-way.
set -euo pipefail

main() {
  local repo="${DEVTOOLBOX_REPO:-conanlk/devtoolbox}"
  local base="https://github.com/$repo/releases"

  local os
  case "$(uname -s)" in
    Darwin) os=macos ;;
    Linux) os=linux ;;
    *) die "unsupported OS $(uname -s); on Windows use install.ps1" ;;
  esac

  if [[ "${DEVTOOLBOX_UNINSTALL:-}" == 1 ]]; then
    [[ "$os" == linux ]] || die "on macOS, quit DevToolBox and move /Applications/DevToolBox.app to the Trash"
    uninstall_linux
    return
  fi

  need curl

  local version="${DEVTOOLBOX_VERSION:-}"
  if [[ -z "$version" ]]; then
    # /releases/latest redirects to /releases/tag/v<version>; no API call, so no rate limit.
    local latest
    latest=$(curl -fsSLI -o /dev/null -w '%{url_effective}' "$base/latest") ||
      die "could not reach $base (is the repository public and does it have a published release?)"
    version="${latest##*/}"
    version="${version#v}"
    [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "no published release found at $base"
  fi
  version="${version#v}"

  local asset
  case "$os" in
    macos) asset="DevToolBox-$version-macos-universal.pkg" ;;
    linux)
      case "$(uname -m)" in
        x86_64 | amd64) asset="DevToolBox-$version-linux-x64.AppImage" ;;
        *) die "no Linux build for $(uname -m) yet (only x86_64)" ;;
      esac
      ;;
  esac

  local tmp
  tmp=$(mktemp -d)
  # Expand now: $tmp is local to main() and no longer exists when the EXIT trap runs.
  # shellcheck disable=SC2064
  trap "rm -rf -- '$tmp'" EXIT

  say "Downloading DevToolBox $version ($asset)"
  curl -fL --progress-bar -o "$tmp/$asset" "$base/download/v$version/$asset" ||
    die "download failed: $base/download/v$version/$asset"
  curl -fsSL -o "$tmp/SHA256SUMS.txt" "$base/download/v$version/SHA256SUMS.txt" ||
    die "this release has no SHA256SUMS.txt, so the download can't be checked"
  verify "$tmp" "$asset"

  case "$os" in
    macos) install_macos "$tmp/$asset" ;;
    linux) install_linux "$tmp/$asset" ;;
  esac
}

verify() {
  local dir="$1" asset="$2" expected actual
  expected=$(awk -v f="$asset" '{ name = $2; sub(/^\*/, "", name) } name == f { print $1 }' "$dir/SHA256SUMS.txt")
  [[ -n "$expected" ]] || die "$asset is not listed in SHA256SUMS.txt"
  if command -v sha256sum >/dev/null; then
    actual=$(sha256sum "$dir/$asset" | awk '{ print $1 }')
  else
    actual=$(shasum -a 256 "$dir/$asset" | awk '{ print $1 }')
  fi
  [[ "$actual" == "$expected" ]] || die "checksum mismatch for $asset (expected $expected, got $actual)"
  say "Checksum OK"
}

install_macos() {
  local pkg="$1"
  # curl doesn't set the quarantine flag; remove it anyway in case a proxy tool or wrapper added one.
  xattr -d com.apple.quarantine "$pkg" 2>/dev/null || true
  say "Installing to /Applications (macOS asks for your password)"
  sudo installer -pkg "$pkg" -target /
  xattr -dr com.apple.quarantine /Applications/DevToolBox.app 2>/dev/null || true
  say "Done. Open DevToolBox from Launchpad or run: open -a DevToolBox"
}

linux_paths() {
  data_dir="${XDG_DATA_HOME:-$HOME/.local/share}"
  app_dir="$data_dir/devtoolbox"
  bin_dir="$HOME/.local/bin"
  appimage="$app_dir/DevToolBox.AppImage"
  desktop_file="$data_dir/applications/devtoolbox.desktop"
  icon_file="$data_dir/icons/hicolor/256x256/apps/devtoolbox.png"
}

install_linux() {
  local downloaded="$1" data_dir app_dir bin_dir appimage desktop_file icon_file
  linux_paths
  mkdir -p "$app_dir" "$bin_dir" "$(dirname "$desktop_file")" "$(dirname "$icon_file")"

  # A fixed path keeps "Open at login" working across updates (it points at this file).
  install -m 755 "$downloaded" "$appimage"
  ln -sf "$appimage" "$bin_dir/devtoolbox"

  # The icon ships inside the AppImage; extracting it doesn't need FUSE.
  local extract
  extract=$(mktemp -d)
  if (cd "$extract" && "$appimage" --appimage-extract devtoolbox.png >/dev/null 2>&1) &&
    [[ -f "$extract/squashfs-root/devtoolbox.png" ]]; then
    install -m 644 "$extract/squashfs-root/devtoolbox.png" "$icon_file"
  fi
  rm -rf "$extract"

  cat >"$desktop_file" <<EOF
[Desktop Entry]
Type=Application
Name=DevToolBox
Comment=Offline developer utilities
Exec="$appimage"
Icon=devtoolbox
Terminal=false
Categories=Development;Utility;
Keywords=json;jwt;base64;hash;uuid;regex;timestamp;
StartupWMClass=DevToolBox
EOF
  command -v update-desktop-database >/dev/null && update-desktop-database "$(dirname "$desktop_file")" 2>/dev/null || true

  say "Installed $appimage"
  case ":$PATH:" in
    *":$bin_dir:"*) say "Run it from your app menu or with: devtoolbox" ;;
    *) say "Run it from your app menu, or add $bin_dir to PATH to use: devtoolbox" ;;
  esac
  if ! ldconfig -p 2>/dev/null | grep -q 'libfuse\.so\.2'; then
    say "Note: AppImages need FUSE 2. If DevToolBox doesn't start, install it (Ubuntu/Debian: sudo apt install libfuse2t64 or libfuse2)."
  fi
}

uninstall_linux() {
  local data_dir app_dir bin_dir appimage desktop_file icon_file
  linux_paths
  rm -f "$bin_dir/devtoolbox" "$desktop_file" "$icon_file"
  rm -rf "$app_dir"
  say "Removed DevToolBox. Your settings are kept in ${XDG_CONFIG_HOME:-$HOME/.config}/DevToolBox (delete them by hand if you want)."
}

need() { command -v "$1" >/dev/null || die "$1 is required"; }
say() { printf '==> %s\n' "$*"; }
die() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

main "$@"
