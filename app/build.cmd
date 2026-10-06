@echo off
rem Builds PSTools.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK, no Visual Studio, no downloads.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" ( echo .NET Framework 4 compiler not found. & exit /b 1 )

if not exist out mkdir out
if not exist app.ico powershell -NoProfile -ExecutionPolicy Bypass -File make-icon.ps1

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:out\PSTools.exe ^
  /win32icon:app.ico /win32manifest:app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:Microsoft.VisualBasic.dll /r:System.Web.Extensions.dll ^
  src\*.cs
if errorlevel 1 exit /b 1

copy /y commands.json out\ >nul
echo.
echo Built out\PSTools.exe
for %%F in (out\PSTools.exe) do echo Size: %%~zF bytes
