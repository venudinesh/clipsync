#!/bin/sh
# Builds ClipSyncAI for Windows: the tests first, then both executables.
#
# No SDK, no NuGet, no MSBuild. The only tool used is the C# compiler that ships
# inside Windows itself, so this runs on a machine with nothing installed beyond
# the .NET Framework that Windows already has. Nothing is downloaded.
#
# Usage:  ./build.sh          tests, then x86 and x64
#         ./build.sh tests    tests only
#         ./build.sh app      executables only

set -e
cd "$(dirname "$0")"

what="${1:-all}"

csc=""
for c in \
    "/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe" \
    "/c/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe"
do
    if [ -x "$c" ]; then csc="$c"; break; fi
done
if [ -z "$csc" ]; then
    echo "No C# compiler found under C:\\Windows\\Microsoft.NET" >&2
    echo "The .NET Framework 4 is missing. It ships with Windows 8 and later," >&2
    echo "and is a free download for Windows 7." >&2
    exit 1
fi

refs="-r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Security.dll -r:System.Windows.Forms.dll"
opts="-nologo -codepage:65001 -optimize+ -warn:4"

mkdir -p build

# The test suite. It is compiled together with the app's own sources because the
# app has no public surface: everything in it is internal, which is the right
# default for a program nothing links against.
if [ "$what" = "all" ] || [ "$what" = "tests" ]; then
    echo "building the test suite"
    rm -f build/tests.exe
    "$csc" $opts -target:exe -platform:anycpu -main:ClipSyncAI.Tests.Runner \
        -out:build/tests.exe $refs 'src\*.cs' 'tests\*.cs'
    echo "running the test suite"
    ./build/tests.exe
fi

if [ "$what" = "tests" ]; then exit 0; fi

# Both executables, from the same sources. The only difference is the word in the
# PE header that tells Windows which machine to load it on.
for arch in x86 x64; do
    out="build/ClipSyncAI-$arch.exe"
    echo "building $out"
    rm -f "$out" "$out.config"
    "$csc" $opts -target:winexe -platform:$arch \
        -win32manifest:app.manifest \
        -win32icon:app.ico \
        -out:"$out" $refs 'src\*.cs'
    cp app.config "$out.config"
done

rm -f build/core.dll build/tests.exe

echo ""
echo "built:"
for f in build/ClipSyncAI-x86.exe build/ClipSyncAI-x64.exe; do
    size=$(wc -c < "$f" | tr -d ' ')
    if command -v sha256sum >/dev/null 2>&1; then
        sum=$(sha256sum "$f" | cut -d' ' -f1)
    else
        sum="sha256sum not available"
    fi
    echo "  $f  $size bytes"
    echo "    $sum"
done
