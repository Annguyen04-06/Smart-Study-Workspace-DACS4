' ==============================================================================
'  SMART STUDY WORKSPACE - KHOI CHAY HE THONG CHAY NGAM (AN HOAN TOAN)
'  Chay daemon-watchdog.ps1 o che do an (khong bat cua so CMD/PowerShell den).
'  Watchdog tu giam sat Backend (cong 5000 + 5500) va AI Service (cong 8000) 24/7.
'  Dung trong: Startup cua Windows (SmartStudyAutoStart.vbs), start-app.bat, run-local-vps.bat.
'  Tat he thong: stop-background.bat
' ==============================================================================
Option Explicit

Dim fileSystem, shell, scriptFolder, watchdogScript, command
Set fileSystem = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

scriptFolder = fileSystem.GetParentFolderName(WScript.ScriptFullName)
watchdogScript = fileSystem.BuildPath(scriptFolder, "daemon-watchdog.ps1")

command = "powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & watchdogScript & """"

' 0 = an hoan toan cua so, False = khong cho tien trinh ket thuc
shell.Run command, 0, False
