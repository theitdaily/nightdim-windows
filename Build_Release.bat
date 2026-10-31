@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
set "RESULT=1"
set "BUILD_OUTPUT=%~dp0NightDim.Setup.build.exe"

if not exist "NightDim.Setup.cs" (
    echo ERROR: NightDim.Setup.cs is missing.
    goto finish
)
if not exist "EULA.txt" (
    echo ERROR: EULA.txt is missing.
    goto finish
)
if not exist "LICENSE" (
    echo ERROR: LICENSE is missing.
    goto finish
)
if not exist "WindowsIntegration.cs" (
    echo ERROR: WindowsIntegration.cs is missing.
    goto finish
)

echo [1/3] Building NightDim.exe...
call "%~dp0Build_NightDim.bat" --no-pause
if errorlevel 1 (
    echo ERROR: NightDim.exe build failed.
    goto finish
)

set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: .NET Framework C# compiler not found.
    goto finish
)

if exist "%BUILD_OUTPUT%" del /q /f "%BUILD_OUTPUT%" >nul 2>&1
if exist "%BUILD_OUTPUT%" (
    echo ERROR: Cannot remove previous temporary setup build.
    goto finish
)

echo [2/3] Embedding NightDim.exe into the self-extracting installer...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /out:"%BUILD_OUTPUT%" /win32icon:"%~dp0NightDim.ico" /resource:"%~dp0NightDim.exe",NightDim.Payload.exe /resource:"%~dp0EULA.txt",NightDim.EULA.txt /resource:"%~dp0LICENSE",NightDim.LICENSE.txt /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "NightDim.Setup.cs" "WindowsIntegration.cs" "AssemblyInfo.cs"
if errorlevel 1 (
    echo ERROR: Installer compilation failed.
    goto finish
)
if not exist "%BUILD_OUTPUT%" (
    echo ERROR: Installer EXE was not created.
    goto finish
)

for /f "delims=" %%V in ('powershell.exe -NoLogo -NoProfile -Command "$v=(Get-Item -LiteralPath 'NightDim.exe').VersionInfo; '{0}.{1}.{2}' -f $v.FileMajorPart,$v.FileMinorPart,$v.FileBuildPart"') do set "VERSION=%%V"
if not defined VERSION (
    echo ERROR: Could not read the NightDim.exe version.
    goto finish
)

set "TARGET=dist\%VERSION%"
if not exist "%TARGET%" mkdir "%TARGET%"
if not exist "%TARGET%" (
    echo ERROR: Could not create %TARGET%.
    goto finish
)

echo [3/3] Preparing the GitHub release file...
copy /Y "%BUILD_OUTPUT%" "%TARGET%\NightDimSetup.exe" >nul
if errorlevel 1 (
    echo ERROR: Could not copy the installer to %TARGET%.
    goto finish
)
if not exist "%TARGET%\NightDimSetup.exe" (
    echo ERROR: Release installer missing.
    goto finish
)

echo SUCCESS: %TARGET%\NightDimSetup.exe
set "RESULT=0"

:finish
if exist "%BUILD_OUTPUT%" del /q /f "%BUILD_OUTPUT%" >nul 2>&1
echo.
if "%RESULT%"=="0" (echo Release prepared successfully.) else (echo Release preparation FAILED.)
echo Press any key to close this window...
pause >nul
endlocal & exit /b %RESULT%
