@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
set "RESULT=1"
set "BUILD_OUTPUT=%~dp0NightDim.build.exe"

set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: The .NET Framework C# compiler was not found.
    echo Install the .NET Framework 4.x Developer Pack or SDK.
    goto finish
)
if not exist "NightDim.ico" (
    echo ERROR: NightDim.ico is missing.
    goto finish
)
if not exist "NightDim.cs" (
    echo ERROR: NightDim.cs is missing.
    goto finish
)
if not exist "UiText.cs" (
    echo ERROR: UiText.cs is missing.
    goto finish
)
if not exist "AssemblyInfo.cs" (
    echo ERROR: AssemblyInfo.cs is missing.
    goto finish
)

if exist "%BUILD_OUTPUT%" del /q /f "%BUILD_OUTPUT%" >nul 2>&1
if exist "%BUILD_OUTPUT%" (
    echo ERROR: Cannot remove the previous temporary build.
    goto finish
)

echo Building NightDim.exe...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /out:"%BUILD_OUTPUT%" /win32icon:"%~dp0NightDim.ico" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "NightDim.cs" "UiText.cs" "AssemblyInfo.cs"
if errorlevel 1 (
    echo ERROR: Compilation failed. Read the compiler errors above.
    goto finish
)
if not exist "%BUILD_OUTPUT%" (
    echo ERROR: Compiler returned success but the EXE was not created.
    goto finish
)

move /y "%BUILD_OUTPUT%" "%~dp0NightDim.exe" >nul
if errorlevel 1 (
    echo ERROR: Cannot update NightDim.exe. Close NightDim if it is running.
    goto finish
)
if not exist "NightDim.exe" (
    echo ERROR: NightDim.exe is missing after the build.
    goto finish
)

echo SUCCESS: NightDim.exe is ready in the project folder.
set "RESULT=0"

:finish
if exist "%BUILD_OUTPUT%" del /q /f "%BUILD_OUTPUT%" >nul 2>&1
echo.
if /i "%~1"=="--no-pause" goto return
echo Press any key to close this window...
pause >nul
:return
endlocal & exit /b %RESULT%
