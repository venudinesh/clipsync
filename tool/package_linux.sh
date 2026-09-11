#!/usr/bin/env bash
# Packages the Flutter Linux release bundle into a .deb, .rpm and a tarball.
# Run after `flutter build linux --release`. Needs: dpkg-deb, rpmbuild, xz.
set -euo pipefail

cd "$(dirname "$0")/.."

BUNDLE="build/linux/x64/release/bundle"
PKG_DIR="dist"
PKG_SRC="packaging/linux"
VERSION="$(grep -m1 '^version:' pubspec.yaml | sed -E 's/version:[[:space:]]*//; s/\+.*//')"
ARTIFACT_PREFIX="clip-sync-ai_${VERSION}"

if [ ! -d "$BUNDLE" ]; then
  echo "error: bundle not found at $BUNDLE — run 'flutter build linux --release' first" >&2
  exit 1
fi

rm -rf "$PKG_DIR"
mkdir -p "$PKG_DIR"

# ---------------------------------------------------------------- .deb
DEB_STAGE="$PKG_DIR/deb-stage"
rm -rf "$DEB_STAGE"
mkdir -p "$DEB_STAGE/usr/lib/clip-sync-ai" \
         "$DEB_STAGE/usr/bin" \
         "$DEB_STAGE/usr/share/applications" \
         "$DEB_STAGE/usr/share/icons/hicolor/512x512/apps" \
         "$DEB_STAGE/DEBIAN"

cp -r "$BUNDLE"/. "$DEB_STAGE/usr/lib/clip-sync-ai/"
ln -s ../lib/clip-sync-ai/clip_sync_ai "$DEB_STAGE/usr/bin/clip-sync-ai"
install -m 0644 "$PKG_SRC/clip-sync-ai.desktop" \
  "$DEB_STAGE/usr/share/applications/clip-sync-ai.desktop"
install -m 0644 "$PKG_SRC/clip_sync_ai.png" \
  "$DEB_STAGE/usr/share/icons/hicolor/512x512/apps/clip_sync_ai.png"
sed "s/__VERSION__/$VERSION/g" "$PKG_SRC/deb/control" > "$DEB_STAGE/DEBIAN/control"

dpkg-deb --root-owner-group --build "$DEB_STAGE" "$PKG_DIR/${ARTIFACT_PREFIX}_amd64.deb"
rm -rf "$DEB_STAGE"
echo "built: dist/${ARTIFACT_PREFIX}_amd64.deb"

# ---------------------------------------------------------------- .rpm
RPM_TOP="$PKG_DIR/rpmbuild"
mkdir -p "$RPM_TOP"/{BUILD,RPMS,SOURCES,SPECS,BUILDROOT}

sed -e "s/__VERSION__/$VERSION/g" \
    -e "s|__BUNDLE__|$PWD/$BUNDLE|g" \
    -e "s|__DESKTOP__|$PWD/$PKG_SRC/clip-sync-ai.desktop|g" \
    -e "s|__ICON__|$PWD/$PKG_SRC/clip_sync_ai.png|g" \
    -e "s|__BUILDROOT__|$PWD/$RPM_TOP/BUILDROOT|g" \
    "$PKG_SRC/rpm/clip-sync-ai.spec" > "$RPM_TOP/SPECS/clip-sync-ai.spec"

rpmbuild --define "_topdir $PWD/$RPM_TOP" -bb "$RPM_TOP/SPECS/clip-sync-ai.spec"

cp "$RPM_TOP"/RPMS/x86_64/*.rpm "$PKG_DIR/${ARTIFACT_PREFIX}.x86_64.rpm"
rm -rf "$RPM_TOP"
echo "built: dist/${ARTIFACT_PREFIX}.x86_64.rpm"

# ---------------------------------------------------------------- tarball
tar -C "$BUNDLE" -cf "$PKG_DIR/clip-sync-ai-linux-x64.tar" .
xz -9 "$PKG_DIR/clip-sync-ai-linux-x64.tar"
echo "built: dist/clip-sync-ai-linux-x64.tar.xz"
