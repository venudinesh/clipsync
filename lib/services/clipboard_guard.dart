import 'package:flutter/services.dart';

/// The text the app itself put on the clipboard, so the next read does not
/// file its own copy as a new clip.
String? _selfCopied;

/// Copies text and remembers it. The clipboard listener and the capture-on-
/// resume path both consult [consumeSelfCopy] before filing anything.
void copyText(String text) {
  _selfCopied = text;
  Clipboard.setData(ClipboardData(text: text));
}

bool consumeSelfCopy(String text) {
  if (_selfCopied == text) {
    _selfCopied = null;
    return true;
  }
  return false;
}
