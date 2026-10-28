@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
set "RESULT=1"

echo Checking files required for the release...
for %%F in ("README.md" "README_RU.md" "README_RS.md" "LICENSE") do (
    if not exist "%%~F" echo ERROR: Missing %%~F
)
if not exist "README.md" goto finish
if not exist "README_RU.md" goto finish
if not exist "README_RS.md" goto finish
if not exist "LICENSE" goto finish

echo.
echo [1/2] Building NightDim.exe...
call "%~dp0Build_NightDim.bat" --no-pause
if errorlevel 1 (
    echo ERROR: Compilation failed. No release archive was created.
    goto finish
)
if not exist "NightDim.exe" (
    echo ERROR: NightDim.exe was not created.
    goto finish
)

echo [2/2] Creating the release ZIP...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $v=(Get-Item -LiteralPath 'NightDim.exe').VersionInfo; $version='{0}.{1}.{2}' -f $v.FileMajorPart,$v.FileMinorPart,$v.FileBuildPart; $name='NightDim-v'+$version+'-win.zip'; $null=New-Item -ItemType Directory -Path 'dist' -Force; $archive=Join-Path 'dist' $name; Compress-Archive -LiteralPath 'NightDim.exe','README.md','README_RU.md','README_RS.md','LICENSE' -DestinationPath $archive -Force; if((-not (Test-Path -LiteralPath $archive)) -or ((Get-Item -LiteralPath $archive).Length -eq 0)) { throw 'ZIP archive is missing or empty' }; $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant(); [System.IO.File]::WriteAllText((Join-Path 'dist' 'SHA256SUMS.txt'), ($hash+'  '+$name+[Environment]::NewLine)); Write-Host ('SUCCESS: '+(Resolve-Path -LiteralPath $archive).Path); Write-Host ('SHA-256: '+$hash)"
if errorlevel 1 (
    echo ERROR: Release packaging failed. Read the PowerShell errors above.
    goto finish
)

echo SUCCESS: Release ZIP is in the dist folder.
set "RESULT=0"

:finish
echo.
if "%RESULT%"=="0" (
    echo Build completed successfully.
) else (
    echo Build FAILED. The errors are shown above.
)
echo Press any key to close this window...
pause >nul
endlocal & exit /b %RESULT%
