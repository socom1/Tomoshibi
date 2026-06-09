#!/usr/bin/env bash
#
# Build a self-contained Tomoshibi.app for macOS.
#
#   ./scripts/pack-mac.sh           # auto-detect arch (arm64 / x64)
#   ./scripts/pack-mac.sh osx-x64   # force Intel
#   ./scripts/pack-mac.sh osx-arm64 # force Apple Silicon
#
# Output: dist/Tomoshibi.app — drag it into /Applications or run
#         `open dist/Tomoshibi.app` to launch.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/Tomoshibi"
DIST="$ROOT/dist"
APP="$DIST/Tomoshibi.app"

RID="${1:-}"
if [ -z "$RID" ]; then
  case "$(uname -m)" in
    arm64) RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) echo "unknown arch: $(uname -m)"; exit 1 ;;
  esac
fi

echo "▸ publishing for $RID ..."
PUBLISH="$DIST/publish-$RID"
rm -rf "$PUBLISH"
dotnet publish "$PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:DebugType=embedded \
  -o "$PUBLISH" \
  > /dev/null

echo "▸ building $APP ..."
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Copy the whole publish output into MacOS/. Avalonia + SkiaSharp need their
# native .dylibs next to the binary at launch; single-file publish doesn't
# extract them reliably on macOS, so we just ship the folder.
cp -R "$PUBLISH"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/Tomoshibi"

# Icon — referenced from Info.plist as "icon" (Apple resolves to icon.icns).
cp "$PROJECT/Assets/icon.icns" "$APP/Contents/Resources/icon.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>            <string>tomoshibi</string>
    <key>CFBundleDisplayName</key>     <string>灯火 · tomoshibi</string>
    <key>CFBundleIdentifier</key>      <string>dev.tomoshibi</string>
    <key>CFBundleExecutable</key>      <string>Tomoshibi</string>
    <key>CFBundleIconFile</key>        <string>icon</string>
    <key>CFBundleVersion</key>         <string>1.0.0</string>
    <key>CFBundleShortVersionString</key><string>1.0.0</string>
    <key>CFBundlePackageType</key>     <string>APPL</string>
    <key>LSMinimumSystemVersion</key>  <string>11.0</string>
    <key>NSHighResolutionCapable</key> <true/>
</dict>
</plist>
PLIST

# Touch the bundle so Finder/Dock refreshes the icon cache for it.
touch "$APP"

echo
echo "✓ $APP"
echo "  drag it into /Applications, or:"
echo "    open '$APP'"
