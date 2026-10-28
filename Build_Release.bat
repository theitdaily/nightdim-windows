@echo off
setlocal
cd /d "%~dp0"
call "%~dp0Build_NightDim.bat"
if errorlevel 1 exit /b 1

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop'; ^
   $info = (Get-Item 'NightDim.exe').VersionInfo; ^
   $version = '{0}.{1}.{2}' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart; ^
   $name = 'NightDim-v' + $version + '-win.zip'; ^
   New-Item -ItemType Directory -Path 'dist' -Force | Out-Null; ^
   $archive = Join-Path 'dist' $name; ^
   Compress-Archive -Force -Path 'NightDim.exe','README.md','README_RU.md','README_RS.md','LICENSE' -DestinationPath $archive; ^
   $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant(); ^
   [System.IO.File]::WriteAllText((Join-Path 'dist' 'SHA256SUMS.txt'), ($hash + '  ' + $name + [Environment]::NewLine)); ^
   Write-Host ('Release ready: ' + $archive); ^
   Write-Host ('SHA-256: ' + $hash)"
if errorlevel 1 (
    echo ERROR: Failed to create release archive.
    if not defined CI pause
    exit /b 1
)
exit /b 0
