' SPDX-License-Identifier: Apache-2.0
' Copyright 2026 Olympium Contributors

Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Text

Namespace OlympiumLinux

    Public Module RuntimeEngine

        Public ReadOnly Property AppsRoot As String
            Get
                Dim home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                Return Path.Combine(home, ".local", "share", "olympium", "apps")
            End Get
        End Property

        Public ReadOnly Property WinePrefix As String
            Get
                Dim home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                Return Path.Combine(home, ".local", "share", "olympium", "wineprefix")
            End Get
        End Property

        Public Function FindExecutable(name As String) As String
            Dim pathEnv = Environment.GetEnvironmentVariable("PATH")
            If String.IsNullOrEmpty(pathEnv) Then Return ""
            For Each dirPath In pathEnv.Split(":"c)
                If String.IsNullOrEmpty(dirPath) Then Continue For
                Dim cand = Path.Combine(dirPath, name)
                If File.Exists(cand) Then Return cand
            Next
            Return ""
        End Function

        Public Function PosixSingleQuote(s As String) As String
            If String.IsNullOrEmpty(s) Then Return "''"
            Return "'" & s.Replace(ChrW(0), "").Replace("'", "'""'""'") & "'"
        End Function

        Public Function RunProcessArgs(exe As String, args As IEnumerable(Of String), Optional envExtra As System.Collections.Generic.Dictionary(Of String, String) = Nothing, Optional waitForExit As Boolean = True, Optional workDir As String = Nothing) As Integer
            Dim psi As New ProcessStartInfo(exe) With {
                .UseShellExecute = False
            }
            If Not String.IsNullOrEmpty(workDir) Then psi.WorkingDirectory = workDir
            For Each a In args
                psi.ArgumentList.Add(a)
            Next
            If envExtra IsNot Nothing Then
                For Each kv In envExtra
                    psi.Environment(kv.Key) = kv.Value
                Next
            End If
            Using p = Process.Start(psi)
                If p Is Nothing Then Return 127
                If Not waitForExit Then Return 0
                p.WaitForExit()
                Return p.ExitCode
            End Using
        End Function

        Public Function RunProcess(exe As String, args As String, Optional envExtra As System.Collections.Generic.Dictionary(Of String, String) = Nothing, Optional waitForExit As Boolean = True) As Integer
            Dim psi As New ProcessStartInfo(exe, args)
            psi.UseShellExecute = False
            If envExtra IsNot Nothing Then
                For Each kv In envExtra
                    psi.Environment(kv.Key) = kv.Value
                Next
            End If
            Using p = Process.Start(psi)
                If p Is Nothing Then Return 127
                If Not waitForExit Then Return 0
                p.WaitForExit()
                Return p.ExitCode
            End Using
        End Function

        Private Sub SafeExtractZip(zipPath As String, destDir As String)
            Dim fullRoot = Path.GetFullPath(destDir).TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar
            Directory.CreateDirectory(destDir)
            Const maxTotalBytes As Long = 256L * 1024L * 1024L
            Const maxEntries As Integer = 8192
            Dim totalBytes As Long = 0
            Dim count As Integer = 0
            Using z = System.IO.Compression.ZipFile.OpenRead(zipPath)
                For Each entry In z.Entries
                    count += 1
                    If count > maxEntries Then Throw New InvalidOperationException("Archive exceeds maximum entry count.")
                    Dim outPath = Path.GetFullPath(Path.Combine(destDir, entry.FullName))
                    If Not outPath.StartsWith(fullRoot, StringComparison.Ordinal) Then
                        Throw New InvalidOperationException("Blocked Zip-Slip path traversal entry: " & entry.FullName)
                    End If
                    If entry.FullName.EndsWith("/") Then
                        Directory.CreateDirectory(outPath)
                    Else
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath))
                        Using src = entry.Open(), dst = File.Create(outPath)
                            Dim buf(8191) As Byte
                            Dim n As Integer
                            Do
                                n = src.Read(buf, 0, buf.Length)
                                If n <= 0 Then Exit Do
                                totalBytes += n
                                If totalBytes > maxTotalBytes Then Throw New InvalidOperationException("Archive exceeds 256 MB safety limit (Zip-Bomb protection).")
                                dst.Write(buf, 0, n)
                            Loop
                        End Using
                    End If
                Next
            End Using
        End Sub

        Private Sub StripSuidBits(stageDir As String)
            Try
                RunProcessArgs("/bin/chmod", {"-R", "u-s,g-s", stageDir}, Nothing, True)
            Catch
            End Try
        End Sub

        Public Function QuoteArg(s As String) As String
            If String.IsNullOrEmpty(s) Then Return """"""
            Return """" & s.Replace("""", "\""") & """"
        End Function

        Public Function ResolveQemuForArch(arch As String) As String
            Dim candidates As String()
            Select Case arch
                Case "arm64" : candidates = {"qemu-aarch64-static", "qemu-aarch64"}
                Case "armhf" : candidates = {"qemu-arm-static", "qemu-arm"}
                Case "x86_64" : candidates = {"qemu-x86_64-static", "qemu-x86_64"}
                Case "i386" : candidates = {"qemu-i386-static", "qemu-i386"}
                Case Else : Return ""
            End Select
            For Each c In candidates
                Dim found = FindExecutable(c)
                If found <> "" Then Return found
            Next
            Return ""
        End Function

        Public Function ResolveSysrootForArch(arch As String) As String
            Dim candidates = {
                "/usr/olympium-cross/" & arch,
                "/usr/" & ArchToTriplet(arch),
                "/lib/" & ArchToTriplet(arch)
            }
            For Each c In candidates
                If Directory.Exists(c) Then Return c
            Next
            Return ""
        End Function

        Private Function ArchToTriplet(arch As String) As String
            Select Case arch
                Case "arm64" : Return "aarch64-linux-gnu"
                Case "armhf" : Return "arm-linux-gnueabihf"
                Case "i386" : Return "i386-linux-gnu"
                Case Else : Return "x86_64-linux-gnu"
            End Select
        End Function

        Public Function OpenAndRun(targetPath As String, ByRef statusMessage As String) As Integer
            Dim info = BinaryInspector.Inspect(targetPath)
            Directory.CreateDirectory(AppsRoot)

            Select Case info.Format
                Case PackageFormat.WindowsExe, PackageFormat.WindowsMsi
                    Return LaunchWindowsApp(info, statusMessage)
                Case PackageFormat.LinuxElf, PackageFormat.AppImage
                    Return LaunchLinuxBinary(info, statusMessage)
                Case PackageFormat.DebianDeb
                    Return InstallAndLaunchDeb(info, statusMessage)
                Case PackageFormat.RpmPackage
                    Return InstallAndLaunchRpm(info, statusMessage)
                Case PackageFormat.MacAppBundle, PackageFormat.MacDmg, PackageFormat.MacPkg, PackageFormat.MacMachO
                    Return LaunchMacApp(info, statusMessage)
                Case PackageFormat.AndroidApk
                    Return InstallAndLaunchApk(info, statusMessage)
                Case PackageFormat.OpenHarmonyHap
                    Return InstallAndLaunchHap(info, statusMessage)
                Case Else
                    statusMessage = "Unsupported file format: " & targetPath
                    Return 2
            End Select
        End Function

        Private Function LaunchWindowsApp(info As InspectionResult, ByRef msg As String) As Integer
            Dim wineBin = FindExecutable("wine")
            If wineBin = "" Then wineBin = FindExecutable("wine64")
            If wineBin = "" Then
                msg = "Wine is not installed. Run: sudo apt install wine wine32 wine64"
                Return 1
            End If

            Directory.CreateDirectory(WinePrefix)
            Dim env As New System.Collections.Generic.Dictionary(Of String, String) From {
                {"WINEPREFIX", WinePrefix},
                {"WINEDEBUG", "-all"}
            }

            ' First-time automatic prefix initialization (silent, zero manual setup)
            If Not File.Exists(Path.Combine(WinePrefix, "system.reg")) Then
                RunProcess(wineBin, "wineboot --init", env, True)
            End If

            Dim execBin = wineBin
            Dim execArgs As String
            If info.Format = PackageFormat.WindowsMsi Then
                execArgs = "msiexec /i " & QuoteArg(info.FilePath)
            Else
                execArgs = QuoteArg(info.FilePath)
            End If

            ' ARM host running x86/x86_64 Windows binary: auto-wrap with box64/box86 or qemu-user-static
            If info.NeedsCrossArch Then
                Dim box = If(info.Architecture = "i386", FindExecutable("box86"), FindExecutable("box64"))
                If box <> "" Then
                    execArgs = QuoteArg(wineBin) & " " & execArgs
                    execBin = box
                Else
                    Dim qemu = ResolveQemuForArch(info.Architecture)
                    If qemu <> "" Then
                        execArgs = QuoteArg(wineBin) & " " & execArgs
                        execBin = qemu
                    End If
                End If
            End If

            msg = "Launched Windows app automatically via Wine (" & info.Architecture & ")"
            Return RunProcess(execBin, execArgs, env, False)
        End Function

        Private Function LaunchLinuxBinary(info As InspectionResult, ByRef msg As String, Optional stageDir As String = Nothing) As Integer
            Try
                RunProcess("/bin/chmod", "+x " & QuoteArg(info.FilePath), Nothing, True)
            Catch
            End Try

            Dim env = StageLibraryEnvironment(stageDir)
            If Not info.NeedsCrossArch Then
                msg = "Running native Linux binary (" & info.Architecture & ")"
                Return RunProcess(info.FilePath, "", env, False)
            End If

            Dim qemu = ResolveQemuForArch(info.Architecture)
            If qemu = "" Then
                msg = "Cross-arch runner missing for " & info.Architecture & ". Install qemu-user-static."
                Return 1
            End If
            Dim sysroot = ResolveSysrootForArch(info.Architecture)
            Dim args = If(sysroot <> "", "-L " & QuoteArg(sysroot) & " " & QuoteArg(info.FilePath), QuoteArg(info.FilePath))
            msg = "Running cross-arch binary via " & Path.GetFileName(qemu) & " (" & info.Architecture & " on " & BinaryInspector.HostArch() & ")"
            Return RunProcess(qemu, args, env, False)
        End Function

        Private Function StageLibraryEnvironment(stageDir As String) As System.Collections.Generic.Dictionary(Of String, String)
            If String.IsNullOrEmpty(stageDir) OrElse Not Directory.Exists(stageDir) Then Return Nothing
            Dim dirs As New System.Collections.Generic.List(Of String)
            For Each root In {stageDir, Path.Combine(stageDir, "usr"), Path.Combine(stageDir, "usr", "local")}
                If Not Directory.Exists(root) Then Continue For
                For Each name In {"lib", "lib64"}
                    Dim libDir = Path.Combine(root, name)
                    If Not Directory.Exists(libDir) Then Continue For
                    dirs.Add(libDir)
                    Try
                        For Each subDir In Directory.GetDirectories(libDir)
                            dirs.Add(subDir)
                        Next
                    Catch
                    End Try
                Next
            Next
            If dirs.Count = 0 Then Return Nothing
            Dim value = String.Join(":", dirs)
            Dim existing = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")
            If Not String.IsNullOrEmpty(existing) Then value &= ":" & existing
            Return New System.Collections.Generic.Dictionary(Of String, String) From {
                {"LD_LIBRARY_PATH", value}
            }
        End Function

        Private Function InstallAndLaunchDeb(info As InspectionResult, ByRef msg As String) As Integer
            Dim dpkg = FindExecutable("dpkg-deb")
            If dpkg = "" Then
                msg = "dpkg-deb not found. Install dpkg."
                Return 1
            End If
            Dim appId = SanitizeId(Path.GetFileNameWithoutExtension(info.FilePath))
            Dim stage = Path.Combine(AppsRoot, "deb-" & appId)
            Directory.CreateDirectory(stage)
            Dim rc = RunProcessArgs(dpkg, {"-x", info.FilePath, stage}, Nothing, True)
            If rc <> 0 Then
                msg = "Failed to extract .deb package (exit " & rc & ")"
                Return rc
            End If
            StripSuidBits(stage)
            Dim bin = FindFirstExtractedBinary(stage)
            If bin <> "" Then
                WriteDesktopShortcut(appId, Path.GetFileNameWithoutExtension(info.FilePath), bin)
                Dim subInfo = BinaryInspector.Inspect(bin)
                Return LaunchLinuxBinary(subInfo, msg, stage)
            End If
            msg = "Extracted .deb into " & stage
            Return 0
        End Function

        Private Function InstallAndLaunchRpm(info As InspectionResult, ByRef msg As String) As Integer
            Dim rpm2cpio = FindExecutable("rpm2cpio")
            Dim cpio = FindExecutable("cpio")
            If rpm2cpio = "" OrElse cpio = "" Then
                msg = "rpm2cpio or cpio not found. Install rpm2cpio and cpio."
                Return 1
            End If
            Dim appId = SanitizeId(Path.GetFileNameWithoutExtension(info.FilePath))
            Dim stage = Path.Combine(AppsRoot, "rpm-" & appId)
            Directory.CreateDirectory(stage)
            ' Direct stream pipeline (NO /bin/sh -c command injection!) + --no-absolute-filenames (prevents path traversal)
            Dim p1Psi As New ProcessStartInfo(rpm2cpio) With {.UseShellExecute = False, .RedirectStandardOutput = True}
            p1Psi.ArgumentList.Add(info.FilePath)
            Dim p2Psi As New ProcessStartInfo(cpio) With {.UseShellExecute = False, .RedirectStandardInput = True, .WorkingDirectory = stage}
            For Each a In {"-idm", "--no-absolute-filenames", "--quiet"}
                p2Psi.ArgumentList.Add(a)
            Next
            Dim rc As Integer = 1
            Using p1 = Process.Start(p1Psi), p2 = Process.Start(p2Psi)
                If p1 IsNot Nothing AndAlso p2 IsNot Nothing Then
                    p1.StandardOutput.BaseStream.CopyTo(p2.StandardInput.BaseStream)
                    p2.StandardInput.Close()
                    p1.WaitForExit()
                    p2.WaitForExit()
                    rc = If(p1.ExitCode = 0 AndAlso p2.ExitCode = 0, 0, 1)
                End If
            End Using
            If rc <> 0 Then
                msg = "Failed to extract .rpm package (exit " & rc & ")"
                Return rc
            End If
            StripSuidBits(stage)
            Dim bin = FindFirstExtractedBinary(stage)
            If bin <> "" Then
                WriteDesktopShortcut(appId, Path.GetFileNameWithoutExtension(info.FilePath), bin)
                Dim subInfo = BinaryInspector.Inspect(bin)
                Return LaunchLinuxBinary(subInfo, msg, stage)
            End If
            msg = "Extracted .rpm into " & stage
            Return 0
        End Function

        Private Function LaunchMacApp(info As InspectionResult, ByRef msg As String) As Integer
            Dim darling = FindExecutable("darling")
            Dim darlingArm64 = If(File.Exists("/opt/olympium/darling-arm64/bin/darling"), "/opt/olympium/darling-arm64/bin/darling", "")
            Dim targetPath = info.FilePath
            Dim launchInfo As InspectionResult = info

            If info.Format = PackageFormat.MacDmg OrElse info.Format = PackageFormat.MacPkg Then
                Dim appId = SanitizeId(Path.GetFileNameWithoutExtension(info.FilePath))
                Dim stage = Path.Combine(AppsRoot, "mac-" & appId)
                Directory.CreateDirectory(stage)
                Dim sevenZ = FindExecutable("7z")
                If sevenZ <> "" Then
                    RunProcess(sevenZ, "x -y -o" & QuoteArg(stage) & " " & QuoteArg(info.FilePath), Nothing, True)
                End If
                Dim foundApp = FindMacAppInDir(stage)
                If foundApp <> "" Then
                    targetPath = foundApp
                    Dim extracted = BinaryInspector.Inspect(foundApp)
                    If extracted.Format = PackageFormat.MacAppBundle OrElse extracted.Format = PackageFormat.MacMachO Then
                        launchInfo = extracted
                    End If
                End If
            End If

            Dim isArmOnly = launchInfo.Architecture.StartsWith("arm64", StringComparison.OrdinalIgnoreCase)
            If isArmOnly Then
                If darlingArm64 = "" Then
                    msg = "Apple Silicon (ARM64-only) Mac app detected. Stage /opt/olympium/darling-arm64 to execute on Linux."
                    Return 1
                End If
                Dim qemu = If(BinaryInspector.HostArch() <> "arm64", ResolveQemuForArch("arm64"), "")
                If qemu <> "" Then
                    msg = "Launching Apple Silicon Mac app via Darling ARM64 + QEMU"
                    Return RunProcess(qemu, QuoteArg(darlingArm64) & " shell open " & QuoteArg(targetPath), Nothing, False)
                End If
                msg = "Launching Apple Silicon Mac app via Darling ARM64"
                Return RunProcess(darlingArm64, "shell open " & QuoteArg(targetPath), Nothing, False)
            End If

            If darling = "" Then
                msg = "Darling GUI is not installed on this Linux host. Install Darling to run Intel/Universal2 Mac apps."
                Return 1
            End If
            msg = "Launching macOS app via Darling GUI (" & launchInfo.Architecture & ")"
            Return RunProcess(darling, "shell open " & QuoteArg(targetPath), Nothing, False)
        End Function

        Private Function InstallAndLaunchApk(info As InspectionResult, ByRef msg As String) As Integer
            Dim adb = FindExecutable("adb")
            Dim scrcpy = FindExecutable("scrcpy")
            If adb = "" Then
                msg = "adb not found. Install slim Android tools: sudo apt install adb scrcpy"
                Return 1
            End If
            Dim rc = RunProcess(adb, "install -r " & QuoteArg(info.FilePath), Nothing, True)
            If rc <> 0 Then
                msg = "No active Android device/container detected via adb (exit " & rc & "). Connect USB/Waydroid/ADB target."
                Return rc
            End If
            If scrcpy <> "" Then
                RunProcess(scrcpy, "--window-title ""Olympium Android""", Nothing, False)
            End If
            msg = "Installed Android APK via slim ADB" & If(scrcpy <> "", " and opened scrcpy window", "")
            Return 0
        End Function

        Private Function InstallAndLaunchHap(info As InspectionResult, ByRef msg As String) As Integer
            Dim hdc = FindExecutable("hdc")
            If hdc = "" AndAlso File.Exists("/opt/olympium/openharmony-slim/hdc") Then
                hdc = "/opt/olympium/openharmony-slim/hdc"
            End If
            If hdc <> "" Then
                Dim rc = RunProcess(hdc, "install -r " & QuoteArg(info.FilePath), Nothing, True)
                If rc = 0 Then
                    msg = "Installed OpenHarmony HAP via slim HDC."
                    Return 0
                End If
            End If
            ' Slim local unpack & manifest inspection without requiring full multi-GB SDK/emulator
            Dim appId = SanitizeId(Path.GetFileNameWithoutExtension(info.FilePath))
            Dim stage = Path.Combine(AppsRoot, "hap-" & appId)
            Directory.CreateDirectory(stage)
            Try
                SafeExtractZip(info.FilePath, stage)
                msg = "Staged OpenHarmony HAP locally to " & stage & " (connect HDC target or slim OpenHarmony runtime to launch)."
                Return 0
            Catch ex As Exception
                msg = "Failed to unpack HAP: " & ex.Message
                Return 1
            End Try
        End Function

        Public Function RegisterDesktopMime(ByRef msg As String) As Integer
            Dim home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            Dim appsDir = Path.Combine(home, ".local", "share", "applications")
            Dim iconsDir = Path.Combine(home, ".local", "share", "icons", "hicolor", "256x256", "apps")
            Directory.CreateDirectory(appsDir)
            Directory.CreateDirectory(iconsDir)

            Dim selfExe = Environment.ProcessPath
            If String.IsNullOrEmpty(selfExe) Then selfExe = "/usr/bin/olympium-linux"

            Dim iconSrc = ResolveDragonIcon()
            If iconSrc <> "" Then
                Try
                    File.Copy(iconSrc, Path.Combine(iconsDir, "olympium.png"), True)
                Catch
                End Try
            End If

            Dim desktopContent =
                "[Desktop Entry]" & vbLf &
                "Type=Application" & vbLf &
                "Name=Olympium" & vbLf &
                "GenericName=Universal Cross-Platform App Runner" & vbLf &
                "Comment=Double-click install and run Windows, macOS, Linux (x86/x64/ARM32/ARM64), Android, and OpenHarmony apps" & vbLf &
                "Exec=" & QuoteArg(selfExe) & " %f" & vbLf &
                "Icon=olympium" & vbLf &
                "Terminal=false" & vbLf &
                "Categories=System;Utility;" & vbLf &
                "MimeType=application/x-ms-dos-executable;application/x-msdownload;application/x-msi;application/vnd.debian.binary-package;application/x-rpm;application/vnd.android.package-archive;application/x-apple-diskimage;application/x-xar;application/x-executable;application/x-sharedlib;application/octet-stream;" & vbLf

            File.WriteAllText(Path.Combine(appsDir, "olympium-linux.desktop"), desktopContent)
            Dim updateDb = FindExecutable("update-desktop-database")
            If updateDb <> "" Then RunProcess(updateDb, QuoteArg(appsDir), Nothing, True)
            msg = "Registered desktop MIME associations in " & appsDir
            Return 0
        End Function

        Public Function ResolveDragonIcon() As String
            Dim baseDir = AppContext.BaseDirectory
            Dim candidates = {
                Path.Combine(baseDir, "assets", "olympium-transparent-1024.png"),
                Path.Combine(baseDir, "assets", "olympium-1024.png"),
                "/usr/share/olympium-linux/olympium-transparent-1024.png",
                "/usr/share/icons/hicolor/256x256/apps/olympium.png",
                Path.Combine(baseDir, "..", "..", "..", "assets", "olympium-transparent-1024.png")
            }
            For Each c In candidates
                If File.Exists(c) Then Return c
            Next
            Return ""
        End Function

        Public Function GetSystemStatusSummary() As String
            Dim sb As New StringBuilder()
            sb.AppendLine("Olympium for Linux — Runtime Capabilities")
            sb.AppendLine("  Host Architecture : " & BinaryInspector.HostArch())
            sb.AppendLine("  Cross-Arch QEMU   : x86=" & StatusTag(ResolveQemuForArch("i386")) &
                          " | x86_64=" & StatusTag(ResolveQemuForArch("x86_64")) &
                          " | arm32=" & StatusTag(ResolveQemuForArch("armhf")) &
                          " | arm64=" & StatusTag(ResolveQemuForArch("arm64")))
            sb.AppendLine("  Windows Auto-Wine : " & StatusTag(FindExecutable("wine")))
            sb.AppendLine("  macOS Darling GUI : " & StatusTag(FindExecutable("darling")) &
                          " | ARM64=" & StatusTag(If(File.Exists("/opt/olympium/darling-arm64/bin/darling"), "/opt/olympium/darling-arm64/bin/darling", "")))
            sb.AppendLine("  Android Slim      : adb=" & StatusTag(FindExecutable("adb")) & " | scrcpy=" & StatusTag(FindExecutable("scrcpy")))
            sb.AppendLine("  OpenHarmony Slim  : hdc=" & StatusTag(FindExecutable("hdc")))
            sb.AppendLine("  Dragon Icon Asset : " & StatusTag(ResolveDragonIcon()))
            Return sb.ToString()
        End Function

        Private Function StatusTag(p As String) As String
            Return If(String.IsNullOrEmpty(p), "missing", "ready (" & Path.GetFileName(p) & ")")
        End Function

        Private Function SanitizeId(s As String) As String
            Dim sb As New StringBuilder()
            For Each ch In s
                If Char.IsLetterOrDigit(ch) OrElse ch = "-"c OrElse ch = "_"c Then
                    sb.Append(Char.ToLowerInvariant(ch))
                Else
                    sb.Append("-"c)
                End If
            Next
            Return If(sb.Length = 0, "app", sb.ToString())
        End Function

        Private Function FindFirstExtractedBinary(stage As String) As String
            Dim searchDirs = {
                Path.Combine(stage, "usr", "bin"),
                Path.Combine(stage, "usr", "local", "bin"),
                Path.Combine(stage, "bin"),
                Path.Combine(stage, "opt")
            }
            For Each d In searchDirs
                If Not Directory.Exists(d) Then Continue For
                For Each f In Directory.GetFiles(d, "*", SearchOption.AllDirectories)
                    Dim info = BinaryInspector.Inspect(f)
                    If info.Format = PackageFormat.LinuxElf Then Return f
                Next
            Next
            Return ""
        End Function

        Private Function FindMacAppInDir(stage As String) As String
            Try
                For Each d In Directory.GetDirectories(stage, "*.app", SearchOption.AllDirectories)
                    Return d
                Next
            Catch
            End Try
            Return ""
        End Function

        Private Sub WriteDesktopShortcut(appId As String, name As String, binPath As String)
            Try
                Dim home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                Dim appsDir = Path.Combine(home, ".local", "share", "applications")
                Directory.CreateDirectory(appsDir)
                Dim selfExe = If(Environment.ProcessPath, "/usr/bin/olympium-linux")
                Dim content =
                    "[Desktop Entry]" & vbLf &
                    "Type=Application" & vbLf &
                    "Name=" & name & vbLf &
                    "Exec=" & QuoteArg(selfExe) & " open " & QuoteArg(binPath) & vbLf &
                    "Icon=olympium" & vbLf &
                    "Terminal=false" & vbLf
                File.WriteAllText(Path.Combine(appsDir, "olympium-app-" & appId & ".desktop"), content)
            Catch
            End Try
        End Sub

    End Module

End Namespace
