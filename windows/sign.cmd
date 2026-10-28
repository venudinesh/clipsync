@echo off
rem Authenticode-sign built executables when a certificate is configured.
rem
rem Configure one of:
rem   CLIPSYNC_PFX            path to a .pfx file
rem   CLIPSYNC_PFX_PASSWORD   its password
rem or:
rem   CLIPSYNC_THUMBPRINT     SHA-1 thumbprint of a certificate in the user store
rem
rem Optional: CLIPSYNC_TIMESTAMP, the RFC 3161 timestamp URL. Defaults to
rem DigiCert's, because an untimestamped signature stops being valid when the
rem certificate expires.
rem
rem With nothing configured this exits 0 without touching the files, so a
rem normal local build stays a normal local build.
rem
rem Usage: sign.cmd build\ClipSyncAI-x64.exe [more files...]

setlocal
if "%~1"=="" (
    echo usage: sign.cmd ^<exe^> [more exes...]
    exit /b 2
)

if not defined CLIPSYNC_PFX if not defined CLIPSYNC_THUMBPRINT (
    echo no signing certificate configured, leaving the build unsigned
    exit /b 0
)

if not defined CLIPSYNC_TIMESTAMP set CLIPSYNC_TIMESTAMP=http://timestamp.digicert.com

if defined CLIPSYNC_THUMBPRINT goto :find_signtool
if not defined CLIPSYNC_PFX_PASSWORD (
    echo CLIPSYNC_PFX is set but CLIPSYNC_PFX_PASSWORD is not.
    exit /b 1
)
if not exist "%CLIPSYNC_PFX%" (
    echo CLIPSYNC_PFX points at "%CLIPSYNC_PFX%", which does not exist.
    exit /b 1
)

:find_signtool
set SIGNTOOL=
for /f "delims=" %%T in ('where signtool.exe 2^>nul') do if not defined SIGNTOOL set SIGNTOOL=%%T
if defined SIGNTOOL goto :have_signtool

set "KITS=%ProgramFiles(x86)%\Windows Kits\10\bin"
if not exist "%KITS%" goto :no_signtool
rem Newest SDK first: the folder names are versions, so sort them descending.
for /f "delims=" %%V in ('dir /b /o-n "%KITS%" 2^>nul') do (
    if not defined SIGNTOOL if exist "%KITS%\%%V\x64\signtool.exe" set "SIGNTOOL=%KITS%\%%V\x64\signtool.exe"
)
if not defined SIGNTOOL goto :no_signtool

:have_signtool
for %%F in (%*) do (
    echo signing %%~nxF
    if defined CLIPSYNC_THUMBPRINT (
        "%SIGNTOOL%" sign /fd SHA256 /tr "%CLIPSYNC_TIMESTAMP%" /td SHA256 /sha1 "%CLIPSYNC_THUMBPRINT%" "%%~F"
    ) else (
        "%SIGNTOOL%" sign /fd SHA256 /tr "%CLIPSYNC_TIMESTAMP%" /td SHA256 /f "%CLIPSYNC_PFX%" /p "%CLIPSYNC_PFX_PASSWORD%" "%%~F"
    )
    if errorlevel 1 exit /b 1
)
exit /b 0

:no_signtool
echo signtool.exe was not found. Install the Windows SDK, or run the build
echo without signing by clearing CLIPSYNC_PFX / CLIPSYNC_THUMBPRINT.
exit /b 1
