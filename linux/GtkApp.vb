' SPDX-License-Identifier: Apache-2.0
' Copyright 2026 Olympium Contributors

Imports System
Imports System.Runtime.InteropServices

Namespace OlympiumLinux

    Public Module GtkApp

        Private Const LibGtk As String = "libgtk-3.so.0"
        Private Const LibGObject As String = "libgobject-2.0.so.0"

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Function gtk_init_check(ByRef argc As Integer, ByRef argv As IntPtr) As Boolean
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Function gtk_window_new(wtype As Integer) As IntPtr
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Sub gtk_window_set_title(win As IntPtr, title As String)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_window_set_default_size(win As IntPtr, w As Integer, h As Integer)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Function gtk_window_set_icon_from_file(win As IntPtr, filename As String, ByRef err As IntPtr) As Boolean
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Function gtk_box_new(orientation As Integer, spacing As Integer) As IntPtr
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_container_set_border_width(container As IntPtr, width As UInteger)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_container_add(container As IntPtr, widget As IntPtr)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_box_pack_start(box As IntPtr, child As IntPtr, expand As Boolean, fill As Boolean, padding As UInteger)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Function gtk_label_new(text As String) As IntPtr
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_label_set_xalign(label As IntPtr, xalign As Single)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Sub gtk_label_set_text(label As IntPtr, text As String)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Function gtk_button_new_with_label(label As String) As IntPtr
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_widget_show_all(widget As IntPtr)
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_main()
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_main_quit()
        End Sub

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Function gtk_file_chooser_dialog_new(title As String, parent As IntPtr, action As Integer,
                                                     firstBtn As String, firstResp As Integer,
                                                     secondBtn As String, secondResp As Integer,
                                                     terminator As IntPtr) As IntPtr
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Function gtk_dialog_run(dialog As IntPtr) As Integer
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Function gtk_file_chooser_get_filename(chooser As IntPtr) As IntPtr
        End Function

        <DllImport(LibGtk, CallingConvention:=CallingConvention.Cdecl)>
        Private Sub gtk_widget_destroy(widget As IntPtr)
        End Sub

        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Sub GtkCallback(widget As IntPtr, data As IntPtr)

        <DllImport(LibGObject, CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
        Private Function g_signal_connect_data(instance As IntPtr, detailedSignal As String, handler As GtkCallback, data As IntPtr, destroyData As IntPtr, flags As Integer) As ULong
        End Function

        Private _pinned As New System.Collections.Generic.List(Of [Delegate])()

        Private Sub Connect(widget As IntPtr, sig As String, cb As GtkCallback)
            _pinned.Add(cb)
            g_signal_connect_data(widget, sig, cb, IntPtr.Zero, IntPtr.Zero, 0)
        End Sub

        Public Function LaunchGui(Optional initialFile As String = "") As Integer
            Dim argc As Integer = 0
            Dim argv As IntPtr = IntPtr.Zero
            Try
                If Not gtk_init_check(argc, argv) Then
                    Console.WriteLine(RuntimeEngine.GetSystemStatusSummary())
                    Console.WriteLine("No graphical display detected; use CLI commands (probe / open / status / register-mime).")
                    Return 0
                End If
            Catch
                Console.WriteLine(RuntimeEngine.GetSystemStatusSummary())
                Return 0
            End Try

            Dim win = gtk_window_new(0)
            gtk_window_set_title(win, "Olympium for Linux — Universal Cross-Platform Runner")
            gtk_window_set_default_size(win, 760, 480)

            Dim iconPath = RuntimeEngine.ResolveDragonIcon()
            If iconPath <> "" Then
                Dim errPtr As IntPtr = IntPtr.Zero
                gtk_window_set_icon_from_file(win, iconPath, errPtr)
            End If

            Dim vbox = gtk_box_new(1, 12)
            gtk_container_set_border_width(vbox, 18)
            gtk_container_add(win, vbox)

            Dim header = gtk_label_new("Olympium for Linux (" & BinaryInspector.HostArch() & ") — Native Double-Click Installer & Cross-Arch Runtime")
            gtk_label_set_xalign(header, 0.0F)
            gtk_box_pack_start(vbox, header, False, False, 0)

            Dim statusLbl = gtk_label_new(RuntimeEngine.GetSystemStatusSummary())
            gtk_label_set_xalign(statusLbl, 0.0F)
            gtk_box_pack_start(vbox, statusLbl, True, True, 0)

            Dim actionLog = gtk_label_new("Ready. Double-click any .exe/.msi/.dmg/.pkg/.deb/.rpm/.apk/.hap/ELF file or click 'Open & Install App...'.")
            gtk_label_set_xalign(actionLog, 0.0F)
            gtk_box_pack_start(vbox, actionLog, False, False, 0)

            Dim hbox = gtk_box_new(0, 10)
            gtk_box_pack_start(vbox, hbox, False, False, 0)

            Dim btnOpen = gtk_button_new_with_label("Open & Install App...")
            Dim btnMime = gtk_button_new_with_label("Register Double-Click File Associations")
            Dim btnClip = gtk_button_new_with_label("Sync Shared Clipboard")
            Dim btnWidgets = gtk_button_new_with_label("Sync Android Widgets")
            Dim btnToggleGuide = gtk_button_new_with_label(If(InteropHub.GetSetting("show_google_play_guide", True), "Hide Google Play Guide", "Show Google Play Guide"))
            gtk_box_pack_start(hbox, btnOpen, False, False, 0)
            gtk_box_pack_start(hbox, btnMime, False, False, 0)
            gtk_box_pack_start(hbox, btnClip, False, False, 0)
            gtk_box_pack_start(hbox, btnWidgets, False, False, 0)
            gtk_box_pack_start(hbox, btnToggleGuide, False, False, 0)

            Dim guideLbl = gtk_label_new(If(InteropHub.GetSetting("show_google_play_guide", True), InteropHub.GooglePlayGuideText(), "Google Play manual setup guide is hidden (click 'Show Google Play Guide' to re-enable)."))
            gtk_label_set_xalign(guideLbl, 0.0F)
            gtk_box_pack_start(vbox, guideLbl, False, False, 0)

            Connect(btnClip, "clicked", Sub(w, d)
                gtk_label_set_text(actionLog, InteropHub.SyncClipboard())
            End Sub)

            Connect(btnWidgets, "clicked", Sub(w, d)
                gtk_label_set_text(actionLog, InteropHub.SyncWidgets())
            End Sub)

            Connect(btnToggleGuide, "clicked", Sub(w, d)
                Dim nextState = Not InteropHub.GetSetting("show_google_play_guide", True)
                InteropHub.SaveSetting("show_google_play_guide", nextState)
                gtk_label_set_text(guideLbl, If(nextState, InteropHub.GooglePlayGuideText(), "Google Play manual setup guide is hidden."))
                gtk_label_set_text(actionLog, If(nextState, "Google Play manual setup guide shown.", "Google Play manual setup guide hidden."))
            End Sub)

            Connect(btnOpen, "clicked", Sub(w, d)
                Dim dlg = gtk_file_chooser_dialog_new("Select App / Package to Install & Run", win, 0,
                                                      "Cancel", -6, "Install & Run", -3, IntPtr.Zero)
                If gtk_dialog_run(dlg) = -3 Then
                    Dim fPtr = gtk_file_chooser_get_filename(dlg)
                    If fPtr <> IntPtr.Zero Then
                        Dim picked = Marshal.PtrToStringUTF8(fPtr)
                        Dim outMsg As String = ""
                        RuntimeEngine.OpenAndRun(picked, outMsg)
                        gtk_label_set_text(actionLog, outMsg)
                    End If
                End If
                gtk_widget_destroy(dlg)
            End Sub)

            Connect(btnMime, "clicked", Sub(w, d)
                Dim outMsg As String = ""
                RuntimeEngine.RegisterDesktopMime(outMsg)
                gtk_label_set_text(actionLog, outMsg)
            End Sub)

            Connect(win, "destroy", Sub(w, d) gtk_main_quit())

            If Not String.IsNullOrEmpty(initialFile) Then
                Dim outMsg As String = ""
                RuntimeEngine.OpenAndRun(initialFile, outMsg)
                gtk_label_set_text(actionLog, outMsg)
            End If

            gtk_widget_show_all(win)
            gtk_main()
            Return 0
        End Function

    End Module

End Namespace
