import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:whisper_flutter_new/whisper_flutter_new.dart';

import 'voice_recorder_service.dart';

/// Manages voice recording + on-device Whisper transcription.
/// Records audio → transcribes locally → returns text.
class VoiceInputService {
  final VoiceRecorderService _recorder = VoiceRecorderService();
  bool _isRecording = false;
  bool _isTranscribing = false;

  bool get isRecording => _isRecording;
  bool get isTranscribing => _isTranscribing;
  bool get isBusy => _isRecording || _isTranscribing;

  /// Live amplitude stream for waveform visualisation (0.0 – 1.0).
  Stream<double> get amplitudeStream => _recorder.amplitudeStream;

  Future<void> init() async {
    await _recorder.init();
  }

  Future<void> dispose() async {
    await _recorder.dispose();
  }

  /// Start recording audio from the microphone.
  Future<void> startRecording() async {
    if (_isRecording) return;
    await _recorder.startRecording();
    _isRecording = true;
    debugPrint('[VoiceInput] Recording started');
  }

  /// Stop recording and transcribe the audio to text using Whisper.
  /// Returns the transcribed text, or null on failure / empty result.
  Future<String?> stopAndTranscribe() async {
    if (!_isRecording) return null;

    String? path;
    try {
      path = await _recorder.stopRecording();
    } finally {
      _isRecording = false;
    }
    debugPrint('[VoiceInput] Recording stopped, file: $path');

    if (path == null || !await File(path).exists()) {
      debugPrint('[VoiceInput] No recording file found');
      return null;
    }

    final fileSize = await File(path).length();
    if (fileSize < 1024) {
      debugPrint('[VoiceInput] Recording too short (${fileSize} bytes)');
      return null;
    }

    // Transcribe with Whisper
    _isTranscribing = true;
    try {
      final text = await _transcribe(path);
      debugPrint('[VoiceInput] Transcribed: ${text?.length ?? 0} chars');
      return text;
    } catch (e) {
      debugPrint('[VoiceInput] Transcription error: $e');
      return null;
    } finally {
      _isTranscribing = false;
      // Clean up the temp file
      try {
        await File(path).delete();
      } catch (_) {}
    }
  }

  /// Cancel recording without transcribing.
  Future<void> cancel() async {
    await _recorder.cancelRecording();
    _isRecording = false;
    _isTranscribing = false;
    debugPrint('[VoiceInput] Cancelled');
  }

  /// Transcribe an audio file using on-device Whisper.
  Future<String?> _transcribe(String audioPath) async {
    try {
      final whisper = Whisper(
        model: WhisperModel.tiny,
        downloadHost:
            'https://hf-mirror.com/ggerganov/whisper.cpp/resolve/main',
      );

      final result = await whisper.transcribe(
        transcribeRequest: TranscribeRequest(
          audio: audioPath,
          isNoTimestamps: true,
          isTranslate: false,
          language: 'en',
        ),
      );

      final text = result.text.trim();
      if (text.isEmpty) return null;
      return text;
    } catch (e) {
      debugPrint('[VoiceInput] Whisper error: $e');
      rethrow;
    }
  }
}
