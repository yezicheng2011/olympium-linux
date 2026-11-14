#!/bin/sh
# Copyright 2026 Ye Zicheng
# SPDX-License-Identifier: Apache-2.0
# Build a slim Linux guest from the unmodified Alpine 3.24.4 minirootfs
# and the matching Alpine virt kernel. The archive is written to dist/.
# It is not committed.
set -eu

cd "$(CDPATH= cd -- "$(dirname "$0")" && pwd)"

for arg in "$@"; do
  case "$arg" in
    *harmonyos*|*HarmonyOS*|*huawei.com*|*HarmonyOS*)
      echo "Refusing a HarmonyOS or Huawei commercial input." >&2
      exit 2
      ;;
    *macos*|*MacOS*|*darwin*|*ios*|*iOS*)
      echo "Refusing an Apple system image. Linkspan does not build one." >&2
      exit 2
      ;;
  esac
done

arch=${1:-$(uname -m)}
version=3.24.4
base="https://dl-cdn.alpinelinux.org/alpine/v3.24/releases/${arch}"

case "$arch" in
  x86_64)
    mini_sha=cdaa38cb8de3537967ee5b6c9b6cd00cc962da2b018e6eea48696bb274923012
    net_sha=1502e776d9ce8cebd5e71f50b9b50b2779757409d4ee471011a56ec8c34951a8
    ;;
  aarch64)
    mini_sha=4b14c18f7e90a063b61462ea1462a5732c154bf6adad594974fe6487cd98a8de
    net_sha=9d10f909d41b8393a80a645e9e2af761fcb1f29b986799edeedfa269c359745e
    ;;
  *)
    echo "No slim Linux build is defined for $arch." >&2
    exit 2
    ;;
esac

need() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing host tool: $1" >&2
    exit 1
  }
}
need curl
need tar
need gzip
need sha256sum
need cpio
need sudo
need gcc
need timeout

work=${WORKDIR:-/tmp/olympium-linux-$arch}
mkdir -p "$work" dist
mini="$work/alpine-minirootfs-${version}-${arch}.tar.gz"
net="$work/alpine-netboot-${version}-${arch}.tar.gz"

fetch() {
  url=$1
  dest=$2
  expect=$3
  if [ ! -f "$dest" ]; then
    curl -fL --retry 3 --retry-delay 2 -o "$dest.partial" "$url"
    mv "$dest.partial" "$dest"
  fi
  got=$(sha256sum "$dest" | awk '{print $1}')
  if [ "$got" != "$expect" ]; then
    echo "SHA-256 mismatch for $dest" >&2
    echo "expected $expect" >&2
    echo "got      $got" >&2
    exit 1
  fi
}

fetch "$base/alpine-minirootfs-${version}-${arch}.tar.gz" "$mini" "$mini_sha"
fetch "$base/alpine-netboot-${version}-${arch}.tar.gz" "$net" "$net_sha"

rm -rf "$work/netboot"
mkdir -p "$work/netboot"
tar -C "$work/netboot" -xzf "$net"
kernel=$(find "$work/netboot" -type f -name 'vmlinuz-virt' | head -n 1)
if [ -z "$kernel" ]; then
  echo "The Alpine netboot archive has no vmlinuz-virt." >&2
  exit 1
fi

stage="$work/stage"
sudo umount "$work/mnt" 2>/dev/null || true
rm -rf "$stage"
mkdir -p "$stage/bin" "$stage/boot" "$work/mnt"
cp "$kernel" "$stage/boot/vmlinuz"
gzip -dc "$mini" > "$stage/rootfs.tar"
printf '%s\n' ide > "$stage/boot/interface"
# The interface file is replaced after the boot probe below.

img="$stage/rootfs.img"
rm -f "$img"
truncate -s 64M "$img"
mkfs.ext4 -F -L olympium-linux "$img" >/dev/null
sudo mount -o loop "$img" "$work/mnt"
sudo tar -C "$work/mnt" -xf "$stage/rootfs.tar"
sudo tee "$work/mnt/olympium-smoke" >/dev/null <<'EOF'
#!/bin/sh
/bin/busybox mount -t proc proc /proc 2>/dev/null || true
echo "ALPINE_RELEASE=$(/bin/busybox cat /etc/alpine-release 2>/dev/null)"
echo "KERNEL=$(/bin/busybox cat /proc/version 2>/dev/null)"
/bin/busybox echo busybox-ok
echo olympium-linux-guest-ok
/bin/busybox sync
/bin/busybox poweroff -f
EOF
sudo chmod 755 "$work/mnt/olympium-smoke"
printf '%s\n' '/dev/vda / ext4 rw,relatime 0 1' | sudo tee "$work/mnt/etc/fstab" >/dev/null
sudo umount "$work/mnt"

modloop=$(find "$work/netboot" -type f -name 'modloop-virt' | head -n 1)
if [ -z "$modloop" ]; then
  echo "The Alpine netboot archive has no modloop-virt." >&2
  exit 1
fi
sudo mkdir -p "$work/modloop"
sudo umount "$work/modloop" 2>/dev/null || true
sudo mount -o loop "$modloop" "$work/modloop"
kdir=$(find "$work/modloop/modules" -mindepth 1 -maxdepth 1 -type d -name '*-virt' | head -n 1)
if [ -z "$kdir" ]; then
  echo "The Alpine module loop has no virt kernel directory." >&2
  exit 1
fi
ird="$work/initramfs-root"
rm -rf "$ird"
mkdir -p "$ird/bin" "$ird/lib" "$ird/modules" "$ird/newroot"
tar -C "$ird" -xf "$stage/rootfs.tar" ./bin/busybox "./lib/ld-musl-${arch}.so.1" "./lib/libc.musl-${arch}.so.1"
for name in mbcache.ko jbd2.ko crc16.ko ext4.ko virtio_blk.ko; do
  found=$(sudo find "$kdir" -type f -name "$name" | head -n 1)
  if [ -z "$found" ]; then
    echo "Alpine virt modules are missing $name." >&2
    exit 1
  fi
  sudo cp "$found" "$ird/modules/$name"
done
sudo chown -R "$(id -u):$(id -g)" "$ird/modules"
cat > "$ird/init" <<'EOF'
#!/bin/busybox sh
/bin/busybox mkdir -p /proc /sys /dev /newroot
/bin/busybox mount -t proc proc /proc
/bin/busybox mount -t sysfs sysfs /sys
/bin/busybox mount -t devtmpfs devtmpfs /dev
fail=0
for mod in mbcache jbd2 crc16 ext4 virtio_blk; do
  if ! /bin/busybox insmod "/modules/${mod}.ko"; then
    echo "insmod ${mod} failed"
    fail=1
  fi
done
if [ "$fail" -ne 0 ]; then
  /bin/busybox poweroff -f
fi
n=0
while [ ! -b /dev/vda ]; do
  n=$((n + 1))
  if [ "$n" -gt 50 ]; then
    echo "virtio disk /dev/vda did not appear"
    /bin/busybox poweroff -f
  fi
  /bin/busybox sleep 0.1
done
/bin/busybox mount -t ext4 /dev/vda /newroot
cmdline=$(/bin/busybox cat /proc/cmdline)
echo "OLYMP_CMDLINE=$cmdline"
guest=$(printf '%s\n' "$cmdline" | /bin/busybox sed -n 's/.*olympium_init=\([^ ]*\).*/\1/p')
if [ -z "$guest" ]; then
  guest=/sbin/init
fi
echo "OLYMP_GUEST=$guest"
if [ ! -x "/newroot$guest" ] && [ ! -L "/newroot$guest" ]; then
  echo "OLYMP_MISSING=$guest"
  /bin/busybox ls -l /newroot/olympium-smoke /newroot/sbin/init || true
fi
exec /bin/busybox switch_root /newroot "$guest"
EOF
chmod 755 "$ird/init"
(cd "$ird" && find . -print0 | cpio --null -o -H newc | gzip -9 > "$stage/boot/initramfs")
if [ ! -s "$stage/boot/initramfs" ]; then
  echo "The initramfs was not created." >&2
  exit 1
fi
sudo umount "$work/modloop"

host_arch=$(uname -m)
surface_cc=${CC:-}
if [ -z "$surface_cc" ]; then
  if [ "$arch" = "$host_arch" ]; then
    surface_cc=gcc
  elif [ "$arch" = aarch64 ]; then
    surface_cc=aarch64-linux-gnu-gcc
  elif [ "$arch" = x86_64 ]; then
    surface_cc=x86_64-linux-gnu-gcc
  else
    echo "No C compiler is known for $arch." >&2
    exit 1
  fi
fi
if ! command -v "$surface_cc" >/dev/null 2>&1; then
  echo "Refusing to pack bin/linkspan-surface for $arch. Install $surface_cc. The host gcc would be the wrong architecture." >&2
  exit 1
fi
"$surface_cc" -O2 -static -o "$stage/bin/linkspan-surface" bridge/linkspan-surface.c
case "$arch" in
  x86_64) file_token=x86-64 ;;
  aarch64) file_token=aarch64 ;;
  *) file_token=$arch ;;
esac
if ! file "$stage/bin/linkspan-surface" | grep -q "$file_token"; then
  echo "bin/linkspan-surface is not a $arch binary." >&2
  file "$stage/bin/linkspan-surface" >&2 || true
  exit 1
fi
cp bridge/linkspan-runtime.sh "$stage/bin/linkspan-runtime"
chmod 755 "$stage/bin/linkspan-runtime"
printf '%s\n' "$arch" > "$stage/ARCH"
cat > "$stage/EDITION" <<EOF
linux-slim 0.1.0
alpine ${version}
arch ${arch}
EOF
cat > "$stage/NOTICE" <<EOF
Linux slim runtime for Linkspan
Copyright 2026 Ye Zicheng for the first-party scripts in this archive.

The root filesystem is the unmodified Alpine Linux ${version} minirootfs.
Alpine packages keep their own licenses. BusyBox is GPL-2.0-only.
The kernel is the unmodified Alpine virt kernel from the Alpine netboot
archive for ${version} (${arch}). It is GPL-2.0-only and is not relicensed.

This is a real Linux root filesystem and kernel. It is not an Alpine
trademark grant, and it is not a Windows, Android, HarmonyOS, macOS, or iOS image.
EOF

probe() {
  if_name=$1
  rootdev=$2
  printf '%s\n' "$if_name" > "$stage/boot/interface"
  log="$work/probe-${if_name}.log"
  # The runtime refuses to run as a graph. Serial output is the guest console.
  if sudo -n timeout 90 "$stage/bin/linkspan-runtime" smoke >"$log" 2>&1; then
    code=0
  else
    code=$?
  fi
  if grep -q 'olympium-linux-guest-ok' "$log" && grep -q 'busybox-ok' "$log"; then
    echo "$if_name $rootdev" > "$work/boot-choice"
    cp "$log" "$work/smoke.log"
    return 0
  fi
  echo "Boot via $if_name did not reach the guest shell (exit $code)." >&2
  tail -n 40 "$log" >&2 || true
  return 1
}

probe virtio /dev/vda

choice=$(cat "$work/boot-choice")
cp "$work/smoke.log" "$stage/SMOKE.txt"
kernel_line=$(grep '^KERNEL=' "$work/smoke.log" | head -n 1 || true)
alpine_line=$(grep '^ALPINE_RELEASE=' "$work/smoke.log" | head -n 1 || true)
cat > "$stage/PROVENANCE" <<EOF
product: linux-slim
edition: 0.1.0
arch: ${arch}
built: $(date -u +%Y-%m-%dT%H:%M:%SZ)

rootfs:
  name: alpine-minirootfs-${version}-${arch}.tar.gz
  url: ${base}/alpine-minirootfs-${version}-${arch}.tar.gz
  sha256: ${mini_sha}
  note: rootfs.tar is that gzip archive uncompressed. The bytes were checked before unpacking.

kernel:
  name: vmlinuz-virt
  from: alpine-netboot-${version}-${arch}.tar.gz
  url: ${base}/alpine-netboot-${version}-${arch}.tar.gz
  sha256: ${net_sha}
  source: https://git.alpinelinux.org/aports/tree/main/linux-virt?h=3.24-stable
  license: GPL-2.0-only
  guest: ${kernel_line}
  initramfs: boot/initramfs contains BusyBox and musl from the minirootfs, plus the unmodified virtio_blk, ext4, jbd2, mbcache, and crc16 modules from the same netboot modloop. It mounts the root filesystem and switches to it.

userspace:
  ${alpine_line}
  license: mixed, per Alpine package. BusyBox is GPL-2.0-only.
  source index: https://dl-cdn.alpinelinux.org/alpine/v3.24/main/${arch}/

boot:
  choice: ${choice}
  smoke: the guest printed busybox-ok and olympium-linux-guest-ok, then powered off.
  display: none. linkspan-surface exits if no guest surface is connected.

Not tested by this build:
  Import into Lima or WSL. This host is Linux and those tools are not the boot path that was run.
EOF

archive="dist/linux-slim-${arch}.tar.gz"
rm -f "$archive"
tar -C "$stage" -czf "$archive" \
  ARCH EDITION NOTICE PROVENANCE SMOKE.txt rootfs.tar rootfs.img \
  boot bin
sha256sum "$archive" | tee "dist/linux-slim-${arch}.sha256"
echo "Wrote $archive"
