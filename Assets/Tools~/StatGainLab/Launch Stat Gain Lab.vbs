Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
command = "cmd /c cd /d """ & scriptDir & """ && dotnet run --project ""StatGainLab.csproj"""
shell.Run command, 0, False
