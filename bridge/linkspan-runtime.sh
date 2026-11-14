#!/bin/sh
# Copyright 2026 Ye Zicheng
# SPDX-License-Identifier: Apache-2.0
# Starts the slim Linux guest. Wine is not used. A nested emulator window is refused.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
arch=$(cat "$root/ARCH")
action=start
init=/sbin/init
memory=512
display=host-window

while [ $# -gt 0 ]; do
  case "$1" in
    start|smoke)
      action=$1
      shift
      ;;
    --memory)
      memory=$2
      shift 2
      ;;
    --display)
      display=$2
      shift 2
      ;;
    --appearance|--graphics|--camera|--gamepad|--quick-boot|--share)
      shift 2
      ;;
    gtk|qemu-window)
      echo "Refusing a nested emulator window." >&2
      exit 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

case "$display" in
  host-window|none) ;;
  *)
    echo "Refusing display '$display'. The guest does not open an emulator frame." >&2
    exit 2
    ;;
esac

if [ "$action" = smoke ]; then
  init=/olympium-smoke
  memory=256
fi

case "$arch" in
  x86_64)
    qemu=qemu-system-x86_64
    console=ttyS0
    machine=
    ;;
  aarch64)
    qemu=qemu-system-aarch64
    console=ttyAMA0
    machine="-machine virt"
    ;;
  *)
    echo "This slim Linux image has no boot path for $arch." >&2
    exit 2
    ;;
esac

if ! command -v "$qemu" >/dev/null 2>&1; then
  echo "$qemu is not installed. Linkspan does not bundle QEMU." >&2
  exit 1
fi

host=$(uname -m)
accel="-accel tcg -cpu max"
if [ "$host" = "$arch" ] && [ -w /dev/kvm ]; then
  accel="-accel kvm -cpu host"
fi

interface=virtio
rootdev=/dev/vda
if [ -f "$root/boot/interface" ]; then
  interface=$(cat "$root/boot/interface")
  if [ "$interface" = ide ]; then
    rootdev=/dev/sda
  fi
fi

initrd_arg=
append="console=$console root=$rootdev rw rootwait panic=1"
if [ -f "$root/boot/initramfs" ]; then
  initrd_arg="-initrd $root/boot/initramfs"
  append="$append olympium_init=$init"
else
  append="$append init=$init"
fi

# shellcheck disable=SC2086
exec "$qemu" \
  -m "$memory" \
  $accel \
  $machine \
  -kernel "$root/boot/vmlinuz" \
  $initrd_arg \
  -append "$append" \
  -drive "file=$root/rootfs.img,format=raw,if=$interface" \
  -display none \
  -serial stdio \
  -monitor none \
  -no-reboot
