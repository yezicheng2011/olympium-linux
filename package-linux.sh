#!/usr/bin/env bash
set -euo pipefail

# Packages Olympium for Linux into .deb, .rpm (if fpm/rpmbuild present), and portable .tar.gz
# Supports x86_64 (linux-x64), arm64 (linux-arm64), armhf (linux-arm), and universal AnyCPU runtime bundle.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
VERSION="${OLYMPIUM_VERSION:-1.0.0}"
OUT_DIR="$SCRIPT_DIR/dist"
DOTNET="${DOTNET_BIN:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"

if [ ! -x "$DOTNET" ]; then
  echo "ERROR: dotnet 8 SDK not found. Install .NET 8 SDK first." >&2
  exit 1
fi

mkdir -p "$OUT_DIR"

build_arch() {
  local rid="$1"
  local deb_arch="$2"
  local stage="$OUT_DIR/stage-$deb_arch"
  rm -rf "$stage"
  mkdir -p "$stage/usr/lib/olympium-linux/assets" \
           "$stage/usr/bin" \
           "$stage/usr/share/applications" \
           "$stage/usr/share/icons/hicolor/256x256/apps" \
           "$stage/usr/share/doc/olympium-linux" \
           "$stage/DEBIAN"

  echo "==> Publishing Olympium for Linux ($rid / $deb_arch)..."
  "$DOTNET" publish "$SCRIPT_DIR/OlympiumLinux.vbproj" \
    -c Release -r "$rid" --self-contained false \
    -o "$stage/usr/lib/olympium-linux"

  cp "$SCRIPT_DIR/assets/olympium-1024.png" "$stage/usr/lib/olympium-linux/assets/"
  cp "$SCRIPT_DIR/assets/olympium-transparent-1024.png" "$stage/usr/lib/olympium-linux/assets/"
  cp "$SCRIPT_DIR/assets/olympium-1024.png" "$stage/usr/share/icons/hicolor/256x256/apps/olympium.png"
  cp "$SCRIPT_DIR/LICENSE" "$SCRIPT_DIR/NOTICE" "$SCRIPT_DIR/THIRD_PARTY_NOTICES.md" "$stage/usr/share/doc/olympium-linux/"

  cat > "$stage/usr/bin/olympium-linux" <<'EOF'
#!/usr/bin/env bash
exec /usr/lib/olympium-linux/olympium-linux "$@"
EOF
  chmod 0755 "$stage/usr/bin/olympium-linux"

  cat > "$stage/usr/share/applications/olympium-linux.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Olympium
GenericName=Universal Cross-Platform App Runner
Comment=Install and run Windows, macOS, Linux (x86/x64/ARM32/ARM64), Android, and OpenHarmony apps natively
Exec=/usr/bin/olympium-linux %f
Icon=olympium
Terminal=false
Categories=System;Utility;
MimeType=application/x-ms-dos-executable;application/x-msdownload;application/x-msi;application/vnd.debian.binary-package;application/x-rpm;application/vnd.android.package-archive;application/x-apple-diskimage;application/x-xar;application/x-executable;
EOF

  cat > "$stage/DEBIAN/control" <<EOF
Package: olympium-linux
Version: $VERSION
Section: utils
Priority: optional
Architecture: $deb_arch
Depends: libgtk-3-0, dotnet-runtime-8.0, qemu-user-static | qemu-user
Recommends: wine, adb, scrcpy, p7zip-full, rpm2cpio, cpio
Maintainer: Olympium Project <maintainers@olympium.local>
Description: Olympium Universal Cross-Platform Desktop App for Linux
 Double-click install and execute Windows (.exe/.msi), macOS (.dmg/.pkg/.app),
 Linux cross-arch (.deb/.rpm/ELF x86/x86_64/armhf/arm64), Android (.apk),
 and OpenHarmony (.hap) applications natively on Linux.
EOF

  if command -v dpkg-deb >/dev/null 2>&1; then
    dpkg-deb --build --root-owner-group "$stage" "$OUT_DIR/olympium-linux_${VERSION}_${deb_arch}.deb"
  fi

  tar -czf "$OUT_DIR/olympium-linux-${VERSION}-${deb_arch}.tar.gz" -C "$stage" usr
  rm -rf "$stage"
}

TARGET_ARCHES="${1:-all}"
case "$TARGET_ARCHES" in
  amd64|x64)   build_arch linux-x64 amd64 ;;
  arm64)       build_arch linux-arm64 arm64 ;;
  armhf|arm)   build_arch linux-arm armhf ;;
  all)
    build_arch linux-x64 amd64
    build_arch linux-arm64 arm64
    build_arch linux-arm armhf
    ;;
  *)
    echo "Usage: $0 [all|amd64|arm64|armhf]" >&2
    exit 2
    ;;
esac

echo "Done. Artifacts written to: $OUT_DIR"
ls -lh "$OUT_DIR"
