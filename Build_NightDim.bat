@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: C# compiler for .NET Framework 4.x was not found.
    echo Install .NET Framework 4.x Developer Pack / SDK.
    if not defined CI pause
    exit /b 1
)
if not exist "NightDim.ico" (
    echo ERROR: NightDim.ico was not found in the source folder.
    if not defined CI pause
    exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /out:"%~dp0NightDim.exe" /win32icon:"%~dp0NightDim.ico" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "NightDim.cs" "UiText.cs" "AssemblyInfo.cs"
if errorlevel 1 (
    echo ERROR: Build failed. See compiler output above.
    if not defined CI pause
    exit /b 1
)
echo Built: NightDim.exe
exit /b 0
