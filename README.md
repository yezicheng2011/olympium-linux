# Olympium for Linux (amd64 / arm64 / armhf)

本目录为 **Olympium for Linux** 独立发布包，采用 VB.NET (.NET 8) + 原生 GTK3 界面构建，支持打包为 `.deb` 与便携 `.tar.gz`，覆盖 `x86_64`、`arm64`、`armhf` 等多架构 Linux 宿主系统。

## 目录内容

- `linux/`：完整 VB.NET (.NET 8) + GTK3 源码、`.deb`/`.tar.gz` 打包脚本、统一金色龙图标及 `LICENSE` / `NOTICE` / `THIRD_PARTY_NOTICES.md`
- `BUILD.txt`：依赖安装、编译与多架构打包命令手册

## 核心能力

1. **双击原生安装并运行全格式软件**：通过 XDG MIME 注册与跨架构执行器，支持 `.exe`/`.msi`（自动后台 Wine，无需手动开启）、`.dmg`/`.pkg`/`.app`（Darling GUI + ARM64 桥接）、`.deb`/`.rpm`/ELF（原生及 x86/x86_64/ARM32/ARM64 `qemu-user-static`）、`.apk`（精简 ADB+scrcpy）与 `.hap`（精简 HDC/安全解包）。
2. **跨生态互联互通 + 内置开源 microG**：支持 Wayland/X11 ↔ Wine ↔ Android ↔ OpenHarmony 双向共享剪切板、统一共享目录 `~/OlympiumShared`、Android 小组件同步，内置开源 microG 核心（Apache-2.0，不含闭源 Play 商店）及界面可一键关闭的 Google Play 手动安装指南。
3. **开源许可**：第一方代码采用 **Apache License 2.0**；第三方组件均保留原许可证（详见 `linux/THIRD_PARTY_NOTICES.md`）。

## 使用方法

本目录已包含源码，请按同级目录的 `BUILD.txt` 执行编译与打包。

## 已知安全风险与残余限制

```text
====================================================================
Olympium for Linux — 已知安全风险与残余限制 (KNOWN_VULNERABILITIES.txt)
====================================================================

一、已在本次审计中直接修复的代码漏洞
--------------------------------------------------------------------
1. [已修复] OS 命令注入 (CWE-78):
   - linux/RuntimeEngine.vb 移除了 /bin/sh -c 拼接执行 rpm2cpio|cpio 的路径，
     改为 ProcessStartInfo.ArgumentList 与直接流管道传递；
   - linux/InteropHub.vb 修复了 adb shell 双引号参数注入，改为严格 POSIX
     单引号转义 + 64 KiB 长度上限。
2. [已修复] Zip-Slip / CPIO 路径穿越与 SUID 提权残留 (CWE-22 / CWE-269):
   - 为 .hap 解包新增 SafeExtractZip（规范路径前缀校验 + 256 MB 防 Zip-Bomb）；
   - 为 .rpm 解包增加 cpio --no-absolute-filenames；
   - 为 .deb / .rpm 解包后强制剥离 SUID/SGID 位 (chmod -R u-s,g-s)。

二、无法单靠本项目代码根除的已知风险
--------------------------------------------------------------------
1. [高] 宿主用户态无强隔离（未套独立 VM/Flatpak 沙箱）
   - 风险：被双击启动的 .exe (Wine)、.deb/.rpm/ELF (QEMU) 与 Darling 程序以
     当前 Linux 桌面用户权限运行，可访问同一用户的 ~/.ssh、浏览器配置与文件。
   - 缓解建议：对不可信软件使用 firejail / bubblewrap 或独立低权限用户运行。

2. [中] 7-Zip / dpkg-deb / cpio / QEMU / Wine / Darling 上游解析漏洞
   - 风险：恶意构造的 .dmg/.pkg/.rpm/.deb 或可执行文件可能触发上游解包器或
     翻译层自身的内存破坏漏洞。
   - 缓解建议：保持宿主发行版安全更新 (sudo apt upgrade)。

3. [中] 跨生态剪贴板与 ADB/HDC 调试通道暴露
   - 风险：共享剪贴板开启时，宿主密码管理器复制的内容可能同步到连接的 Android
     或鸿蒙设备；ADB/HDC 开启无线调试时若在不可信局域网存在接管风险。
   - 缓解建议：仅使用 USB 或本机回环 ADB/HDC，复制敏感信息时暂停剪贴板同步。
```
