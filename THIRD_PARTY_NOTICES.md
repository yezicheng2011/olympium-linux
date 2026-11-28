# Third-Party Notices — Olympium for Linux

## First-Party Code
All first-party VB.NET source code and packaging scripts in this repository are licensed under **Apache License, Version 2.0** ([LICENSE](LICENSE), [NOTICE](NOTICE)).

## Third-Party Components & External Runtimes

| Component | Role on Linux | License | Distribution / Link Model |
| :--- | :--- | :--- | :--- |
| **.NET 8 Runtime & BCL** | Executes `olympium-linux` | **MIT** | Host runtime dependency |
| **GTK 3 & GLib / GObject** (`libgtk-3.so.0`, `libgobject-2.0.so.0`) | Native Linux desktop window & file chooser | **LGPL-2.1-or-later** | Dynamically loaded via P/Invoke at runtime (`dlopen`) |
| **Linux kernel (`binfmt_misc`)** | Host OS kernel & binary format dispatch | **GPL-2.0-only** | Host OS kernel; not modified or bundled |
| **QEMU** (`qemu-user-static`, host package) | Cross-arch `x86`/`x86_64`/`arm32`/`arm64` execution | **GPL-2.0-only** | Declared as package `Depends`; invoked as standalone process |
| **Wine** (`wine`, `wine32`, `wine64`, host package) | Automatic `.exe`/`.msi` execution | **LGPL-2.1-or-later** | Declared as `Recommends`; invoked as standalone process |
| **Box64 / Box86** (optional host package on ARM) | Fast x86/x86_64 Wine translation on ARM hosts | **MIT** | Invoked externally when present |
| **Darling** (optional host package) | macOS Intel / Universal2 / ARM64 execution | **GPL-3.0-only** (+ **APSL-2.0** / BSD / MIT submodules) | Invoked externally when installed |
| **dpkg-deb**, **rpm2cpio**, **GNU cpio**, **7-Zip (`7z`)** | Package extraction for `.deb`/`.rpm`/`.dmg`/`.pkg` | **GPL-2.0-or-later** / **GPL-3.0-or-later** / **LGPL-2.1-or-later & BSD** | Invoked as separate child processes |
| **Android `adb` & `scrcpy`** (host package) | Slim Android `.apk` install, clipboard sync, AppWidget query & display window | **Apache-2.0** | Invoked as standalone processes |
| **microG Services Core (`GmsCore`)** | Open-source GMS APIs for Android runtime (no proprietary Play Store bundled) | **Apache-2.0** | Optional APK staged/installed via ADB |
| **OpenHarmony `hdc`** (optional host tool) | Slim OpenHarmony `.hap` install | **Apache-2.0** | Invoked as standalone process |

> **Compliance Note**: `.deb` and `.tar.gz` packages install `LICENSE`, `NOTICE`, and `THIRD_PARTY_NOTICES.md` into `/usr/share/doc/olympium-linux/`.
