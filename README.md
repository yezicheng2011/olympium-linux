# Slim Linux runtime for Linkspan

A real Alpine Linux 3.24.4 root filesystem and the matching Alpine virt kernel, for a virtual machine on a host that is not already Linux. Linux hosts run Linux programs directly and do not need this image.

The scripts in this repository are Apache-2.0, Copyright 2026 Ye Zicheng. The image is not committed. Linkspan downloads a published GitHub Release at runtime, chooses GitHub or a China mirror from the client IP, and checks SHA-256.

The official site is https://olympium.net. Do not commit `*.img`, `*.tar.gz`, certificates, or keys.

## What the archive contains

`build-slim.sh` downloads the unmodified Alpine minirootfs and netboot archives, checks the SHA-256 values published by Alpine, and refuses to continue if they differ. It builds an ext4 disk, a small initramfs with BusyBox plus the virtio and ext4 modules from that same netboot archive, and boots it with QEMU. The boot must print `busybox-ok` and `olympium-linux-guest-ok` before the archive is written.

The guest is Alpine Linux. BusyBox is GPL-2.0-only. The kernel is GPL-2.0-only. Neither is relicensed as Apache-2.0. Source locations are in `PROVENANCE` inside the archive.

## Build

```bash
./build-slim.sh x86_64
./build-slim.sh aarch64
```

The host needs `curl`, `cpio`, `gcc`, `sudo`, and QEMU. An aarch64 image can be built and booted with QEMU's software CPU on an x86_64 host. That is not a substitute for a boot on aarch64 hardware.

## What was tested for 0.1.0

| Asset | Boot | Where |
| --- | --- | --- |
| `linux-slim-x86_64.tar.gz` | Alpine 3.24.4, kernel 6.18.60-0-virt, guest shell, then power off | QEMU with KVM on Linux x86_64 |
| `linux-slim-aarch64.tar.gz` | Alpine 3.24.4, kernel 6.18.60-0-virt, guest shell, then power off | QEMU TCG on a Linux x86_64 host |

Not tested: importing the root filesystem into Lima or WSL, a boot on aarch64 hardware, and `bin/linkspan-surface` on an aarch64 host. That helper is compiled with the build host's gcc, so in the aarch64 archive it is an x86_64 Linux executable. The guest kernel and root filesystem in that archive are aarch64. The boot script does not run the helper.

## Assets

- `linux-slim-x86_64.tar.gz`
- `linux-slim-aarch64.tar.gz`
