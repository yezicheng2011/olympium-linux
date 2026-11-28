# Olympium for Linux (amd64 / arm64 / armhf)

项目主页：https://olympium.net

本仓库为 **Olympium for Linux** 源码，采用 VB.NET (.NET 8) + 原生 GTK3 界面构建，支持打包为 `.deb` 与便携 `.tar.gz`，覆盖 `x86_64`、`arm64`、`armhf` 等多架构 Linux 宿主系统。

## 目录内容

- `OlympiumLinux.vbproj`、`.vb` 源码、`package-linux.sh`、`assets/`：完整 VB.NET (.NET 8) + GTK3 源码、`.deb`/`.tar.gz` 打包脚本、统一金色龙图标
- `LICENSE` / `NOTICE` / `THIRD_PARTY_NOTICES.md`：Apache License 2.0 与第三方组件许可
- `BUILD.txt`：依赖安装、编译与多架构打包命令手册
- `KNOWN_VULNERABILITIES.txt`：已修复漏洞清单与无法单靠代码根除的残余安全风险说明

## 核心能力

1. **双击原生安装并运行全格式软件**：通过 XDG MIME 注册与跨架构执行器，支持 `.exe`/`.msi`（自动后台 Wine，无需手动开启）、`.dmg`/`.pkg`/`.app`（Darling GUI + ARM64 桥接）、`.deb`/`.rpm`/ELF（原生及 x86/x86_64/ARM32/ARM64 `qemu-user-static`）、`.apk`（精简 ADB+scrcpy）与 `.hap`（精简 HDC/安全解包）。
2. **跨生态互联互通 + 内置开源 microG**：支持 Wayland/X11 ↔ Wine ↔ Android ↔ OpenHarmony 双向共享剪切板、统一共享目录 `~/OlympiumShared`、Android 小组件同步，内置开源 microG 核心（Apache-2.0，不含闭源 Play 商店）及界面可一键关闭的 Google Play 手动安装指南。
3. **开源许可**：第一方代码采用 **Apache License 2.0**；第三方组件均保留原许可证（详见 `THIRD_PARTY_NOTICES.md`）。

## 使用方法

在仓库根目录按 `BUILD.txt` 执行编译与打包。
