Name:       clip-sync-ai
Version:    __VERSION__
Release:    1
Summary:    Clipboard note-taking and transcription with on-device AI
License:    Proprietary
URL:        https://clipsync.local
Group:      Applications/System
BuildArch:  x86_64
Requires:   gtk3, libsecret, zenity
BuildRoot:  __BUILDROOT__

%description
ClipSyncAI turns whatever you copy into editable notes, recognizes voice
with on-device Whisper, and lets you chat with a local or cloud model.

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}/usr/lib/clip-sync-ai
cp -r __BUNDLE__/. %{buildroot}/usr/lib/clip-sync-ai/
mkdir -p %{buildroot}/usr/bin
ln -s ../lib/clip-sync-ai/clip_sync_ai %{buildroot}/usr/bin/clip-sync-ai
mkdir -p %{buildroot}/usr/share/applications
install -m 0644 __DESKTOP__ %{buildroot}/usr/share/applications/clip-sync-ai.desktop
mkdir -p %{buildroot}/usr/share/icons/hicolor/512x512/apps
install -m 0644 __ICON__ %{buildroot}/usr/share/icons/hicolor/512x512/apps/clip_sync_ai.png

%post
gtk-update-icon-cache -f -q /usr/share/icons/hicolor 2>/dev/null || :
update-desktop-database /usr/share/applications 2>/dev/null || :

%postun
gtk-update-icon-cache -f -q /usr/share/icons/hicolor 2>/dev/null || :
update-desktop-database /usr/share/applications 2>/dev/null || :

%files
/usr/lib/clip-sync-ai/*
/usr/bin/clip-sync-ai
/usr/share/applications/clip-sync-ai.desktop
/usr/share/icons/hicolor/512x512/apps/clip_sync_ai.png

%changelog
