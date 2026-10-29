@echo off
rem Builds ClipSyncAI for Windows: the tests first, then both executables.
rem
rem No SDK, no NuGet, no MSBuild. By default the only tool used is the C#
rem compiler that ships inside Windows itself, so this runs on a machine with
rem nothing installed beyond the .NET Framework that Windows already has.
rem Nothing is downloaded. That compiler only speaks C# 5: set CSC to a Roslyn
rem csc.exe (Visual Studio, Build Tools) to build the same sources with C# 7.3.
rem The target stays .NET Framework 4.0 either way.
rem
rem Usage:  build.cmd          tests, then x86 and x64
rem         build.cmd tests    tests only
rem         build.cmd app      executables only
rem         build.cmd build-x64-release   x64 release build only (no tests)

setlocal
cd /d "%~dp0"

set WHAT=%1
if "%WHAT%"=="" set WHAT=all

set LANGVER=5
if defined CSC goto :csc_given
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo No C# compiler found under %WINDIR%\Microsoft.NET
    echo The .NET Framework 4 is missing. It ships with Windows 8 and later,
    echo and is a free download for Windows 7.
    exit /b 1
)
goto :csc_ready

:csc_given
if not exist "%CSC%" (
    echo CSC points at "%CSC%", which does not exist.
    exit /b 1
)
set LANGVER=7.3

:csc_ready
set REFS=-r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Security.dll -r:System.Windows.Forms.dll
set OPTS=-nologo -codepage:65001 -optimize+ -warn:4 -langversion:%LANGVER%
echo using C# %LANGVER%: %CSC%

rem The llama.cpp runtime rides inside the executable as embedded resources and
rem is written out on first use (NativeLoader). The names are the plain file
rem names, which is what GetManifestResourceStream looks up.
set RES=
for %%F in (native\*.dll) do call :res "%%F"

if not exist build mkdir build

if "%WHAT%"=="app" goto app
if "%WHAT%"=="build-x64-release" goto build_x64_release

rem The test suite is compiled together with the app's own sources because the app
rem has no public surface: everything in it is internal, which is the right
rem default for a program nothing links against.
echo building the test suite
if exist build\tests.exe del /q build\tests.exe
"%CSC%" %OPTS% -target:exe -platform:anycpu -main:ClipSyncAI.Tests.Runner -out:build\tests.exe %REFS% src\*.cs tests\*.cs
if errorlevel 1 exit /b 1
echo running the test suite
build\tests.exe
if errorlevel 1 exit /b 1

if "%WHAT%"=="tests" goto done

:app
for %%A in (x86 x64) do (
    call :one %%A
    if errorlevel 1 exit /b 1
)
if exist build\core.dll del /q build\core.dll
if exist build\tests.exe del /q build\tests.exe

rem Signing is a no-op unless a certificate is configured; see sign.cmd.
call sign.cmd "build\ClipSyncAI-x86.exe" "build\ClipSyncAI-x64.exe"
if errorlevel 1 exit /b 1

echo.
echo built:
for %%F in (build\ClipSyncAI-x86.exe build\ClipSyncAI-x64.exe) do echo   %%F  %%~zF bytes
certutil -hashfile build\ClipSyncAI-x86.exe SHA256
certutil -hashfile build\ClipSyncAI-x64.exe SHA256
goto done

:build_x64_release
echo building x64 release only
mkdir build
set OUT=build\ClipSyncAI-x64.exe
echo building %OUT%
if exist "%OUT%" del /q "%OUT%"
"%CSC%" %OPTS% -target:winexe -platform:x64 -win32manifest:app.manifest -win32icon:app.ico -out:"%OUT%" %REFS% %RES% src\*.cs
if errorlevel 1 exit /b 1
copy /y app.config "%OUT%.config" >nul
exit /b 0

rem Both executables come from the same sources. The only difference is the word
rem in the PE header that tells Windows which machine to load it on.
:one
set OUT=build\ClipSyncAI-%1.exe
echo building %OUT%
if exist "%OUT%" del /q "%OUT%"
"%CSC%" %OPTS% -target:winexe -platform:%1 -win32manifest:app.manifest -win32icon:app.ico -out:"%OUT%" %REFS% %RES% src\*.cs
if errorlevel 1 exit /b 1
copy /y app.config "%OUT%.config" >nul
exit /b 0

:res
rem Appends "-resource:<path>,<file name>" for one runtime DLL. The path is
rem quoted inside the switch because the sources can live under a folder whose
rem name has spaces.
set R=%~1
set RES=%RES% -resource:"%R%",%~nx1
exit /b 0

:done
endlocal
