@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
set "RESULT=1"

if not exist "NightDim.exe" (
    echo ERROR: NightDim.exe not found. Run Build_NightDim.bat first.
    goto finish
)

for /f "delims=" %%V in ('powershell.exe -NoLogo -NoProfile -Command "$v=(Get-Item -LiteralPath 'NightDim.exe').VersionInfo; '{0}.{1}.{2}' -f $v.FileMajorPart,$v.FileMinorPart,$v.FileBuildPart"') do set "VERSION=%%V"
if not defined VERSION (
    echo ERROR: Could not read the version from NightDim.exe.
    goto finish
)

set "TARGET=dist\%VERSION%"
if not exist "%TARGET%" mkdir "%TARGET%"
if not exist "%TARGET%" (
    echo ERROR: Could not create %TARGET%.
    goto finish
)

copy /Y "NightDim.exe" "%TARGET%\NightDim.exe" >nul
if errorlevel 1 (
    echo ERROR: Could not copy NightDim.exe to %TARGET%.
    goto finish
)

echo SUCCESS: %TARGET%\NightDim.exe
set "RESULT=0"

:finish
echo.
if "%RESULT%"=="0" (echo Release prepared successfully.) else (echo Release preparation FAILED.)
echo Press any key to close this window...
pause >nul
endlocal & exit /b %RESULT%
