@echo off
powershell "Start-Process -FilePath powershell.exe -ArgumentList '%~fs0\..\Scripts\Uninstaller.ps1', 'AlteryxAddIns.Roslyn' -verb RunAs"
