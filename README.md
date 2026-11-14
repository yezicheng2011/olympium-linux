# Slim Linux runtime for Linkspan

A small Linux root filesystem for a virtual machine on Windows or macOS. Linux hosts run Linux programs directly and do not need this image.

The scripts here are Apache-2.0, Copyright 2026 Ye Zicheng. The image they build is not committed. Linkspan downloads a published GitHub Release of this repository at runtime. A China mirror is selected by Linkspan from the client IP. The mirror must serve the same bytes. Linkspan checks SHA-256.

The official site is https://olympium.net. Do not commit `*.img`, `*.tar.gz`, certificates, or keys.

## Build

```bash
./build-slim.sh
```


1. Build a minimal root filesystem with a shell and nothing else you do not need.
2. Pack it as `rootfs.tar` inside the asset named below, plus `bin/linkspan-runtime`.
3. The runtime command imports the root filesystem into the virtual machine the user already has (Lima or WSL). It does not download a distribution on its own.


After a build, fill `sha256` in `runtime-lock.json`, set `"published": true` in this file and in Linkspan's `share/runtime-lock.json`, tag `v0.1.0`, and attach the `.tar.gz` assets to the GitHub Release. Until that release exists, Linkspan reports that the image is not published.

## Assets

- `linux-slim-x86_64.tar.gz`
- `linux-slim-aarch64.tar.gz`
