#!/usr/bin/env bash
# Builds artifacts/installers/DevToolBox-<version>-macos-universal.pkg on a Mac.
#
#   build/installers/macos/build-pkg.sh
#
# Publishes Native AOT for osx-arm64 and osx-x64, merges every Mach-O file into a universal DevToolBox.app (lipo),
# and wraps it in an installer package that puts the app in /Applications.
# Needs Xcode or the Command Line Tools (pkgbuild, productbuild, lipo, codesign).
#
# The package is unsigned: on another Mac, Gatekeeper blocks it until the user allows it in
# System Settings › Privacy & Security › "Open Anyway". Signing needs an Apple Developer ID (see docs/installers.md).
set -euo pipefail

repo="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$repo"

project=src/DevToolBox.App
publish_root=artifacts/publish
work=artifacts/installers/macos-work
out=artifacts/installers
bundle_id=com.devtoolbox.app

[[ "$(uname -s)" == Darwin ]] || { echo "error: run this on macOS" >&2; exit 1; }

version=$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"([0-9]+\.[0-9]+\.[0-9]+)".*/\1/p' version.json)

# The first publish bumps the build number in version.json; the second reuses it so both halves match.
echo "==> Publishing osx-arm64"
dotnet publish "$project" -c Release -r osx-arm64 -o "$publish_root/osx-arm64"
build=$(sed -nE 's/.*"build"[[:space:]]*:[[:space:]]*([0-9]+).*/\1/p' version.json)
echo "==> Publishing osx-x64 (build $build)"
dotnet publish "$project" -c Release -r osx-x64 -o "$publish_root/osx-x64" -p:BuildNumber="$build"

echo "==> Merging into a universal DevToolBox.app"
rm -rf "$work"
mkdir -p "$work/root" "$out"
app="$work/root/DevToolBox.app"
cp -R "$publish_root/osx-arm64/DevToolBox.app" "$app"

while IFS= read -r -d '' file; do
  rel="${file#"$app"/}"
  x64="$publish_root/osx-x64/DevToolBox.app/$rel"
  lipo -info "$file" >/dev/null 2>&1 || continue          # not a Mach-O file
  if [[ ! -f "$x64" ]]; then
    echo "error: $rel has no osx-x64 counterpart" >&2; exit 1
  fi
  if lipo -archs "$file" | grep -qw x86_64; then
    continue                                              # NuGet native libs that already ship universal
  fi
  lipo -create "$file" "$x64" -output "$file.universal"
  mv "$file.universal" "$file"
done < <(find "$app/Contents/MacOS" -type f -print0)

chmod +x "$app/Contents/MacOS/DevToolBox"
codesign --force --deep --sign - "$app"
echo "    $(lipo -archs "$app/Contents/MacOS/DevToolBox")"

echo "==> Checking the universal app"
"$app/Contents/MacOS/DevToolBox" --smoke-exit | tee "$work/smoke.log"
grep -q "tools_ok=" "$work/smoke.log"

echo "==> Building the installer package"
# Not relocatable: always install to /Applications, even if another copy of the bundle exists elsewhere.
pkgbuild --analyze --root "$work/root" "$work/component.plist" >/dev/null
plutil -replace 0.BundleIsRelocatable -bool NO "$work/component.plist"
pkgbuild --root "$work/root" \
  --component-plist "$work/component.plist" \
  --identifier "$bundle_id" \
  --version "$version.$build" \
  --install-location /Applications \
  "$work/DevToolBox-component.pkg"

sed -e "s/{Version}/$version/g" -e "s/{BundleIdentifier}/$bundle_id/g" \
  build/installers/macos/distribution.xml > "$work/distribution.xml"

pkg="$out/DevToolBox-$version-macos-universal.pkg"
productbuild --distribution "$work/distribution.xml" \
  --package-path "$work" \
  "$pkg"

rm -rf "$work"
echo "==> $pkg ($(du -h "$pkg" | cut -f1))"
