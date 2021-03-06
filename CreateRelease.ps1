﻿$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSCommandPath
Push-Location $root

if (Test-Path .\Release) {
    Remove-Item .\Release -Recurse -Force
}

$vsPath = .\vswhere.exe -latest -requires 'Microsoft.Component.MSBuild' -property installationPath
if ($vsPath -eq $null) {
    Write-Host "Failed to find Visual Studio Install"
    Pop-Location
    exit -1
}
$vsPath = Join-Path $vsPath 'MSBuild\15.0\Bin\MSBuild.exe'

.\nuget.exe restore .\OmniBus.AddIns.sln
& $vsPath .\OmniBus.AddIns.sln /t:Rebuild /p:Configuration=Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build Failed"
    Pop-Location
    exit -1
}

New-Item -Path . -name "Release" -type directory

Copy-Item .\AlteryxAddIns\bin\Release .\Release -Recurse
Remove-Item .\Release\Release\Scripts -Recurse
Get-ChildItem .\Release\Release\*.bat | Remove-Item
Rename-Item .\Release\Release OmniBus

Copy-Item .\AlteryxAddIns.Roslyn\bin\Release .\Release -Recurse
Remove-Item .\Release\Release\Scripts -Recurse
Get-ChildItem .\Release\Release\*.bat | Remove-Item
Rename-Item .\Release\Release OmniBus.Roslyn

Copy-Item .\OmniBus.XmlTools\bin\Release .\Release -Recurse
Remove-Item .\Release\Release\Scripts -Recurse
Get-ChildItem .\Release\Release\*.bat | Remove-Item
Rename-Item .\Release\Release OmniBus.XmlTools

Copy-Item .\OmniBusRegex .\Release -Recurse
Get-ChildItem .\Release\OmniBusRegex\*.bat | Remove-Item

Copy-Item .\Scripts .\Release -Recurse
Get-ChildItem .\Release\Scripts\*.bat | Move-Item -Destination .\Release

& .\Release\Install.bat

.\RunUnitTests.ps1
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    exit -1
}

$version = Read-Host "Enter version number (e.g. 1.3.2)"
while ($version -notmatch '^\d+\.\d+\.?\d*$') {
    $version = Read-Host "Invalid Version. Enter version number (e.g. 1.3.2)"
}

Get-ChildItem *.bak -Recurse | Remove-Item -Force

Write-Host "Building ReadMe.docx ..."
& pandoc -f markdown_github -t docx README.md -o README.docx
if ($LASTEXITCODE -ne 0) {
    $message = "Documentation failed (you need pandoc): " + $LASTEXITCODE
    Write-Host $message
    Pop-Location
    exit -1
}

Write-Host "Running Word to create pdf ..."
$word = New-Object -ComObject "Word.Application"
$doc = $word.Documents.Open("$root/Readme.docx")
$doc.ExportAsFixedFormat("$root/README.pdf", 17, 0)
$doc.Close(0)
$word.Quit([ref]0)
Remove-Item "$root\README.docx"

$output = "$root\AlteryxOmniBus v$version.zip"
Compress-Archive -Path ("$root\README.pdf", "$root\Release\*") -DestinationPath $output -Verbose -Update
Remove-Item "$root\README.pdf"

$output = "$root\AlteryxOmniBus Tests v$version.zip"
Compress-Archive -Path ("$root\RunUnitTests.ps1", "$root\RunUnitTests.yxmd", "$root\Test Workflows") -DestinationPath $output -Verbose -Update

Pop-Location