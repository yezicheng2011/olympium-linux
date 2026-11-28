' SPDX-License-Identifier: Apache-2.0
' Copyright 2026 Olympium Contributors

Imports System
Imports System.IO

Namespace OlympiumLinux

    Public Module Program

        Public Function Main(args As String()) As Integer
            If args Is Nothing OrElse args.Length = 0 Then
                Return GtkApp.LaunchGui()
            End If

            Dim cmd = args(0).ToLowerInvariant()
            Select Case cmd
                Case "--help", "-h", "help"
                    PrintHelp()
                    Return 0
                Case "status"
                    Console.Write(RuntimeEngine.GetSystemStatusSummary())
                    Console.WriteLine("  Shared Clipboard  : " & If(InteropHub.GetSetting("clipboard_sync_enabled", True), "enabled", "disabled"))
                    Console.WriteLine("  Desktop Widgets   : " & If(InteropHub.GetSetting("android_widgets_enabled", True), "enabled", "disabled"))
                    Console.WriteLine("  Android microG    : Apache-2.0 GmsCore (no Play Store bundled)")
                    Console.WriteLine("  Google Play Guide : " & If(InteropHub.GetSetting("show_google_play_guide", True), "shown (turn off via: play-guide off)", "hidden"))
                    Return 0
                Case "clip-sync"
                    Dim txt = If(args.Length > 1, String.Join(" ", args, 1, args.Length - 1), Nothing)
                    Console.WriteLine(InteropHub.SyncClipboard(txt))
                    Return 0
                Case "widgets"
                    Console.WriteLine(InteropHub.SyncWidgets())
                    Return 0
                Case "microg-setup"
                    Console.WriteLine(InteropHub.ProvisionMicroG())
                    Return 0
                Case "play-guide"
                    Dim act = If(args.Length > 1, args(1).ToLowerInvariant(), "show")
                    If act = "off" OrElse act = "hide" Then
                        InteropHub.SaveSetting("show_google_play_guide", False)
                        Console.WriteLine("Google Play manual setup guide hidden in Settings.")
                    ElseIf act = "on" OrElse act = "show" Then
                        InteropHub.SaveSetting("show_google_play_guide", True)
                        Console.WriteLine(InteropHub.GooglePlayGuideText())
                    Else
                        Console.WriteLine("show_google_play_guide=" & InteropHub.GetSetting("show_google_play_guide", True).ToString().ToLowerInvariant())
                    End If
                    Return 0
                Case "register-mime"
                    Dim msg As String = ""
                    Dim rc = RuntimeEngine.RegisterDesktopMime(msg)
                    Console.WriteLine(msg)
                    Return rc
                Case "probe"
                    If args.Length < 2 Then
                        Console.Error.WriteLine("Usage: olympium-linux probe <file>")
                        Return 2
                    End If
                    Dim info = BinaryInspector.Inspect(args(1))
                    Console.WriteLine("File         : " & info.FilePath)
                    Console.WriteLine("Format       : " & info.Format.ToString())
                    Console.WriteLine("Target OS    : " & info.TargetOs)
                    Console.WriteLine("Architecture : " & info.Architecture & If(info.Bits > 0, " (" & info.Bits & "-bit)", ""))
                    Console.WriteLine("Cross-Arch   : " & info.NeedsCrossArch.ToString().ToLowerInvariant())
                    Console.WriteLine("Plan         : " & info.ExecutionPlan)
                    If info.Notes <> "" Then Console.WriteLine("Notes        : " & info.Notes)
                    Return 0
                Case "open", "install", "run"
                    If args.Length < 2 Then
                        Console.Error.WriteLine("Usage: olympium-linux " & cmd & " <file>")
                        Return 2
                    End If
                    Dim msg As String = ""
                    Dim rc = RuntimeEngine.OpenAndRun(args(1), msg)
                    Console.WriteLine(msg)
                    Return rc
                Case Else
                    ' Direct double-click path argument
                    If File.Exists(args(0)) OrElse Directory.Exists(args(0)) Then
                        Dim msg As String = ""
                        Dim rc = RuntimeEngine.OpenAndRun(args(0), msg)
                        Console.WriteLine(msg)
                        Return rc
                    End If
                    Console.Error.WriteLine("Unknown command: " & args(0))
                    PrintHelp()
                    Return 2
            End Select
        End Function

        Private Sub PrintHelp()
            Console.WriteLine("Olympium for Linux — Universal Cross-Platform Application Suite")
            Console.WriteLine("Usage:")
            Console.WriteLine("  olympium-linux                       Launch GTK3 desktop app")
            Console.WriteLine("  olympium-linux open <file>           Double-click install & run (.exe/.msi/.dmg/.pkg/.app/.deb/.rpm/.apk/.hap/ELF)")
            Console.WriteLine("  olympium-linux probe <file>          Inspect binary format, target OS, and architecture")
            Console.WriteLine("  olympium-linux register-mime         Register native double-click desktop MIME handlers")
            Console.WriteLine("  olympium-linux status                Show host architecture, QEMU, Wine, Darling, ADB/scrcpy, and HDC status")
        End Sub

    End Module

End Namespace
