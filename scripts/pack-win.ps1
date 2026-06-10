# Build a self-contained Tomoshibi folder + zip for Windows.
#
#   .\scripts\pack-win.ps1            # x64
#   .\scripts\pack-win.ps1 win-arm64  # ARM
#
# Output: dist\Tomoshibi-win-<arch>.zip — unzip anywhere and run Tomoshibi.exe.
# The EXE icon comes from Assets\icon.ico via the csproj.
#
# NOTE: written on macOS and not yet run on a real Windows box — if anything
# misbehaves, the underlying command is just `dotnet publish` + Compress-Archive.

param([string]$Rid = "win-x64")

$ErrorActionPreference = "Stop"

$Root    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Project = Join-Path $Root "src\Tomoshibi"
$Dist    = Join-Path $Root "dist"
$Publish = Join-Path $Dist "publish-$Rid"
$Zip     = Join-Path $Dist "Tomoshibi-$Rid.zip"

Write-Host "publishing for $Rid ..."
if (Test-Path $Publish) { Remove-Item -Recurse -Force $Publish }
dotnet publish $Project -c Release -r $Rid --self-contained true -p:DebugType=embedded -o $Publish | Out-Null

Write-Host "zipping ..."
if (Test-Path $Zip) { Remove-Item -Force $Zip }
Compress-Archive -Path "$Publish\*" -DestinationPath $Zip

Write-Host ""
Write-Host "done: $Zip"
