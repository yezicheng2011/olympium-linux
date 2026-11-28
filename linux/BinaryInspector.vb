' SPDX-License-Identifier: Apache-2.0
' Copyright 2026 Olympium Contributors

Imports System
Imports System.IO
Imports System.IO.Compression
Imports System.Text

Namespace OlympiumLinux

    Public Enum PackageFormat
        Unknown
        LinuxElf
        DebianDeb
        RpmPackage
        AppImage
        WindowsExe
        WindowsMsi
        MacDmg
        MacPkg
        MacAppBundle
        MacMachO
        AndroidApk
        OpenHarmonyHap
    End Enum

    Public Class InspectionResult
        Public Property FilePath As String = ""
        Public Property Format As PackageFormat = PackageFormat.Unknown
        Public Property TargetOs As String = "Unknown"
        Public Property Architecture As String = "unknown"
        Public Property Bits As Integer = 0
        Public Property DisplayName As String = ""
        Public Property ExecutionPlan As String = ""
        Public Property NeedsCrossArch As Boolean = False
        Public Property Notes As String = ""
    End Class

    Public Module BinaryInspector

        Public Function HostArch() As String
            Select Case System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
                Case System.Runtime.InteropServices.Architecture.X64 : Return "x86_64"
                Case System.Runtime.InteropServices.Architecture.X86 : Return "i386"
                Case System.Runtime.InteropServices.Architecture.Arm64 : Return "arm64"
                Case System.Runtime.InteropServices.Architecture.Arm : Return "armhf"
                Case Else : Return "x86_64"
            End Select
        End Function

        Public Function Inspect(path As String) As InspectionResult
            Dim res As New InspectionResult()
            res.FilePath = path
            res.DisplayName = System.IO.Path.GetFileName(path)
            If String.IsNullOrEmpty(path) Then Return res

            If Directory.Exists(path) AndAlso path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) Then
                res.Format = PackageFormat.MacAppBundle
                res.TargetOs = "macOS"
                InspectMacAppBundle(path, res)
                Return res
            End If

            If Not File.Exists(path) Then
                res.Notes = "File does not exist."
                Return res
            End If

            Dim ext = System.IO.Path.GetExtension(path).ToLowerInvariant()
            Dim hdr(4095) As Byte
            Dim nRead As Integer = 0
            Try
                Using fs = File.OpenRead(path)
                    nRead = fs.Read(hdr, 0, hdr.Length)
                End Using
            Catch ex As Exception
                res.Notes = "Read error: " & ex.Message
                Return res
            End Try

            ' 1. Extension + container checks
            If ext = ".deb" Then
                res.Format = PackageFormat.DebianDeb
                res.TargetOs = "Linux"
                res.Architecture = GuessArchFromFilename(path, HostArch())
                res.Bits = If(res.Architecture = "i386" OrElse res.Architecture = "armhf", 32, 64)
                res.NeedsCrossArch = (res.Architecture <> HostArch())
                res.ExecutionPlan = If(res.NeedsCrossArch, "Sandboxed dpkg-deb extract + qemu-user-static (" & res.Architecture & ")", "Sandboxed dpkg-deb extract + native launch")
                Return res
            End If

            If ext = ".rpm" Then
                res.Format = PackageFormat.RpmPackage
                res.TargetOs = "Linux"
                res.Architecture = GuessArchFromFilename(path, HostArch())
                res.Bits = If(res.Architecture = "i386" OrElse res.Architecture = "armhf", 32, 64)
                res.NeedsCrossArch = (res.Architecture <> HostArch())
                res.ExecutionPlan = If(res.NeedsCrossArch, "Sandboxed rpm2cpio extract + qemu-user-static (" & res.Architecture & ")", "Sandboxed rpm2cpio extract + native launch")
                Return res
            End If

            If ext = ".msi" OrElse (nRead >= 8 AndAlso hdr(0) = &HD0 AndAlso hdr(1) = &HCF AndAlso hdr(2) = &H11 AndAlso hdr(3) = &HE0) Then
                res.Format = PackageFormat.WindowsMsi
                res.TargetOs = "Windows"
                res.Architecture = GuessArchFromFilename(path, "x86_64")
                res.Bits = If(res.Architecture = "i386", 32, 64)
                res.NeedsCrossArch = (HostArch() = "arm64" OrElse HostArch() = "armhf")
                res.ExecutionPlan = "Auto-Wine (msiexec /i) with prebuilt prefix"
                Return res
            End If

            If ext = ".apk" OrElse ext = ".hap" Then
                InspectZipMobile(path, ext, res)
                Return res
            End If

            If ext = ".dmg" Then
                res.Format = PackageFormat.MacDmg
                res.TargetOs = "macOS"
                res.Architecture = GuessArchFromFilename(path, "universal2")
                res.Bits = 64
                res.ExecutionPlan = "Confined 7z/hfs unpack -> Darling GUI (Intel/Universal2/ARM64 bridge)"
                Return res
            End If

            If ext = ".pkg" Then
                res.Format = PackageFormat.MacPkg
                res.TargetOs = "macOS"
                res.Architecture = GuessArchFromFilename(path, "universal2")
                res.Bits = 64
                res.ExecutionPlan = "Confined xar/cpio unpack -> Darling GUI (Intel/Universal2/ARM64 bridge)"
                Return res
            End If

            ' 2. Magic byte inspection: ELF
            If nRead >= 20 AndAlso hdr(0) = &H7F AndAlso hdr(1) = Asc("E"c) AndAlso hdr(2) = Asc("L"c) AndAlso hdr(3) = Asc("F"c) Then
                res.Format = If(ext = ".appimage", PackageFormat.AppImage, PackageFormat.LinuxElf)
                res.TargetOs = "Linux"
                Dim elfClass = hdr(4)
                Dim isLittle = (hdr(5) = 1)
                res.Bits = If(elfClass = 1, 32, 64)
                Dim eMachine As UShort = If(isLittle, BitConverter.ToUInt16(hdr, 18), CUShort((CInt(hdr(18)) << 8) Or hdr(19)))
                Select Case eMachine
                    Case 3 : res.Architecture = "i386"
                    Case 62 : res.Architecture = "x86_64"
                    Case 40 : res.Architecture = "armhf"
                    Case 183 : res.Architecture = "arm64"
                    Case Else : res.Architecture = "elf-" & eMachine.ToString()
                End Select
                res.NeedsCrossArch = Not IsNativeCompatible(HostArch(), res.Architecture)
                res.ExecutionPlan = If(res.NeedsCrossArch,
                    "Cross-arch qemu-user-static (" & res.Architecture & " on " & HostArch() & ")",
                    "Native Linux kernel execution (" & res.Architecture & ")")
                Return res
            End If

            ' 3. Magic byte inspection: Windows PE (.exe / .dll)
            If nRead >= 64 AndAlso hdr(0) = Asc("M"c) AndAlso hdr(1) = Asc("Z"c) Then
                res.Format = PackageFormat.WindowsExe
                res.TargetOs = "Windows"
                InspectPeMachine(path, res)
                res.NeedsCrossArch = (HostArch() = "arm64" OrElse HostArch() = "armhf") AndAlso (res.Architecture = "x86_64" OrElse res.Architecture = "i386")
                If res.NeedsCrossArch Then
                    res.ExecutionPlan = "Auto-Wine via Box64/Box86 or qemu-user-static (" & res.Architecture & " on " & HostArch() & ")"
                Else
                    res.ExecutionPlan = "Auto-Wine background execution (zero manual setup)"
                End If
                Return res
            End If

            ' 4. Magic byte inspection: Mach-O / Universal Fat Binary
            If nRead >= 16 Then
                Dim m32 As UInteger = (CUInt(hdr(0)) << 24) Or (CUInt(hdr(1)) << 16) Or (CUInt(hdr(2)) << 8) Or CUInt(hdr(3))
                If m32 = &HCAFEBABEUI OrElse m32 = &HBEBAFECAUI OrElse m32 = &HFEEDFACEUI OrElse m32 = &HCEFAEDFEUI OrElse m32 = &HFEEDFACFUI OrElse m32 = &HCFFAEDFEUI Then
                    res.Format = PackageFormat.MacMachO
                    res.TargetOs = "macOS"
                    InspectMachO(hdr, nRead, res)
                    Return res
                End If
            End If

            res.Notes = "Unrecognized binary format."
            Return res
        End Function

        Private Sub InspectPeMachine(path As String, res As InspectionResult)
            res.Architecture = "x86_64"
            res.Bits = 64
            Try
                Using fs = File.OpenRead(path)
                    If fs.Length < 64 Then Return
                    Dim buf(3) As Byte
                    fs.Seek(60, SeekOrigin.Begin)
                    If fs.Read(buf, 0, 4) < 4 Then Return
                    Dim peOff = BitConverter.ToInt32(buf, 0)
                    If peOff < 0 OrElse peOff + 6 > fs.Length Then Return
                    Dim pe(5) As Byte
                    fs.Seek(peOff, SeekOrigin.Begin)
                    If fs.Read(pe, 0, 6) < 6 Then Return
                    If pe(0) = Asc("P"c) AndAlso pe(1) = Asc("E"c) AndAlso pe(2) = 0 AndAlso pe(3) = 0 Then
                        Dim mach = BitConverter.ToUInt16(pe, 4)
                        Select Case mach
                            Case &H14C : res.Architecture = "i386" : res.Bits = 32
                            Case &H8664 : res.Architecture = "x86_64" : res.Bits = 64
                            Case &H1C0, &H1C4 : res.Architecture = "armhf" : res.Bits = 32
                            Case &HAA64 : res.Architecture = "arm64" : res.Bits = 64
                        End Select
                    End If
                End Using
            Catch
            End Try
        End Sub

        Private Sub InspectMachO(hdr As Byte(), nRead As Integer, res As InspectionResult)
            Dim mBE As UInteger = (CUInt(hdr(0)) << 24) Or (CUInt(hdr(1)) << 16) Or (CUInt(hdr(2)) << 8) Or CUInt(hdr(3))
            If mBE = &HCAFEBABEUI Then
                Dim nfat As Integer = CInt((CUInt(hdr(4)) << 24) Or (CUInt(hdr(5)) << 16) Or (CUInt(hdr(6)) << 8) Or CUInt(hdr(7)))
                Dim hasX64 As Boolean = False
                Dim hasArm64 As Boolean = False
                Dim hasI386 As Boolean = False
                For i = 0 To Math.Min(nfat, 8) - 1
                    Dim off = 8 + i * 20
                    If off + 4 > nRead Then Exit For
                    Dim cputype As UInteger = (CUInt(hdr(off)) << 24) Or (CUInt(hdr(off + 1)) << 16) Or (CUInt(hdr(off + 2)) << 8) Or CUInt(hdr(off + 3))
                    If cputype = &H1000007UI Then hasX64 = True
                    If cputype = &H100000CUI Then hasArm64 = True
                    If cputype = 7UI Then hasI386 = True
                Next
                If hasX64 AndAlso hasArm64 Then
                    res.Architecture = "universal2 (x86_64 + arm64)"
                    res.Bits = 64
                    res.ExecutionPlan = "Darling GUI (Intel x86_64 slice + ARM64 bridge)"
                ElseIf hasArm64 Then
                    res.Architecture = "arm64 (Apple Silicon)"
                    res.Bits = 64
                    res.NeedsCrossArch = (HostArch() <> "arm64")
                    res.ExecutionPlan = "Darling ARM64 bridge (/opt/olympium/darling-arm64)"
                ElseIf hasX64 Then
                    res.Architecture = "x86_64 (Intel Mac)"
                    res.Bits = 64
                    res.NeedsCrossArch = (HostArch() <> "x86_64")
                    res.ExecutionPlan = "Darling GUI (Intel x86_64)"
                ElseIf hasI386 Then
                    res.Architecture = "i386 (Intel 32-bit Mac)"
                    res.Bits = 32
                    res.ExecutionPlan = "Darling (i386)"
                Else
                    res.Architecture = "fat-macho"
                    res.Bits = 64
                    res.ExecutionPlan = "Darling GUI"
                End If
                Return
            End If

            Dim cputypeLE As UInteger = BitConverter.ToUInt32(hdr, 4)
            Select Case cputypeLE
                Case &H1000007UI
                    res.Architecture = "x86_64 (Intel Mac)"
                    res.Bits = 64
                    res.NeedsCrossArch = (HostArch() <> "x86_64")
                    res.ExecutionPlan = "Darling GUI (Intel x86_64)"
                Case &H100000CUI
                    res.Architecture = "arm64 (Apple Silicon)"
                    res.Bits = 64
                    res.NeedsCrossArch = (HostArch() <> "arm64")
                    res.ExecutionPlan = "Darling ARM64 bridge (/opt/olympium/darling-arm64)"
                Case 7UI
                    res.Architecture = "i386 (Intel 32-bit Mac)"
                    res.Bits = 32
                    res.ExecutionPlan = "Darling (i386)"
                Case Else
                    res.Architecture = "mach-o"
                    res.Bits = 64
                    res.ExecutionPlan = "Darling GUI"
            End Select
        End Sub

        Private Sub InspectMacAppBundle(appDir As String, res As InspectionResult)
            Dim macosDir = System.IO.Path.Combine(appDir, "Contents", "MacOS")
            res.Architecture = "universal2"
            res.Bits = 64
            res.ExecutionPlan = "Darling GUI (.app bundle)"
            If Directory.Exists(macosDir) Then
                For Each f In Directory.GetFiles(macosDir)
                    Dim subRes = Inspect(f)
                    If subRes.Format = PackageFormat.MacMachO Then
                        res.Architecture = subRes.Architecture
                        res.Bits = subRes.Bits
                        res.ExecutionPlan = subRes.ExecutionPlan
                        res.NeedsCrossArch = subRes.NeedsCrossArch
                        Exit For
                    End If
                Next
            End If
        End Sub

        Private Sub InspectZipMobile(path As String, ext As String, res As InspectionResult)
            If ext = ".hap" Then
                res.Format = PackageFormat.OpenHarmonyHap
                res.TargetOs = "OpenHarmony"
                res.Architecture = "multi-abi"
                res.Bits = 64
                res.ExecutionPlan = "Slim OpenHarmony HDC / local HAP runner (no full OS image)"
            Else
                res.Format = PackageFormat.AndroidApk
                res.TargetOs = "Android"
                res.Architecture = "multi-abi"
                res.Bits = 64
                res.ExecutionPlan = "Slim Android ADB + scrcpy mirror (no full OS image)"
            End If
            Try
                Using z = ZipFile.OpenRead(path)
                    Dim abis As New System.Collections.Generic.HashSet(Of String)()
                    For Each e In z.Entries
                        If e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) OrElse e.FullName.StartsWith("libs/", StringComparison.OrdinalIgnoreCase) Then
                            Dim parts = e.FullName.Split("/"c)
                            If parts.Length >= 3 Then abis.Add(parts(1))
                        End If
                    Next
                    If abis.Count > 0 Then
                        res.Architecture = String.Join(", ", abis)
                    End If
                End Using
            Catch
            End Try
        End Sub

        Private Function GuessArchFromFilename(path As String, fallback As String) As String
            Dim lower = System.IO.Path.GetFileName(path).ToLowerInvariant()
            If lower.Contains("arm64") OrElse lower.Contains("aarch64") Then Return "arm64"
            If lower.Contains("armhf") OrElse lower.Contains("armv7") OrElse lower.Contains("armeabi") Then Return "armhf"
            If lower.Contains("i386") OrElse lower.Contains("i686") OrElse lower.Contains("win32") OrElse lower.Contains("x86.") Then Return "i386"
            If lower.Contains("amd64") OrElse lower.Contains("x86_64") OrElse lower.Contains("x64") Then Return "x86_64"
            Return fallback
        End Function

        Public Function IsNativeCompatible(host As String, target As String) As Boolean
            If host = target Then Return True
            If host = "x86_64" AndAlso target = "i386" Then Return True
            If host = "arm64" AndAlso target = "armhf" Then Return True
            Return False
        End Function

    End Module

End Namespace
