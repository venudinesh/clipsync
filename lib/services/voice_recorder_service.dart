import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:path_provider/path_provider.dart';
import 'package:record/record.dart';

/// Wraps the `record` package to provide:
/// - Start / stop recording
/// - Live amplitude stream for waveform visualisation
/// - Output file path for downstream transcription
class VoiceRecorderService {
  final AudioRecorder _recorder = AudioRecorder();

  /// The on-device Whisper engine decodes 16 kHz mono 16-bit WAV only
  /// (its native bridge reads WAV via dr_wav), so recordings must be captured
  /// in that exact format to transcribe without a conversion step.
  static final bool _recordWhisperFormat =
      defaultTargetPlatform == TargetPlatform.android;

  /// Whisper's expected sample rate: 16 kHz.
  static const int _whisperSampleRate = 16000;

  StreamSubscription<Amplitude>? _ampSub;
  final _ampController = StreamController<double>.broadcast();
  bool _isRecording = false;
  String? _outputPath;

  /// Live normalised amplitude (0.0 → 1.0).
  Stream<double> get amplitudeStream => _ampController.stream;
  bool get isRecording => _isRecording;
  String? get outputPath => _outputPath;

  // ── Lifecycle ─────────────────────────────────────────────────────────

  Future<void> init() async {
    if (await _recorder.hasPermission()) {
      // Permission granted — ready to record
    }
  }

  Future<void> dispose() async {
    await _ampSub?.cancel();
    await _ampController.close();
    await _recorder.dispose();
  }

  // ── Recording ─────────────────────────────────────────────────────────

  /// Start recording into the app's temp directory.
  Future<void> startRecording() async {
    if (_isRecording) return;

    // Android records 16 kHz mono WAV, the format the on-device Whisper engine
    // decodes. Everywhere else keep the AAC file (m4a) that other decoders
    // accept.
    final dir = await getTemporaryDirectory();
    final timestamp = DateTime.now().millisecondsSinceEpoch;
    final outputName = _recordWhisperFormat
        ? 'voice_note_$timestamp.wav'
        : 'voice_note_$timestamp.m4a';
    _outputPath = '${dir.path}/$outputName';

    final encoder =
        _recordWhisperFormat ? AudioEncoder.wav : AudioEncoder.aacLc;
    final config = _recordWhisperFormat
        ? RecordConfig(
            encoder: encoder,
            sampleRate: _whisperSampleRate,
            numChannels: 1,
          )
        : RecordConfig(
            encoder: encoder,
            bitRate: 128000,
            sampleRate: 44100,
            numChannels: 1,
          );

    await _recorder.start(
      config,
      path: _outputPath!,
    );

    _isRecording = true;

    // Stream amplitude for waveform visualisation
    _ampSub = _recorder.onAmplitudeChanged(const Duration(milliseconds: 100))
        .listen((amp) {
      // Normalise dB values: silence ≈ -50 dB, loud ≈ 0 dB
      final normalized = ((amp.current + 50) / 50).clamp(0.0, 1.0);
      _ampController.add(normalized);
    });
  }

  /// Stop recording and return the output file path.
  Future<String?> stopRecording() async {
    if (!_isRecording) return _outputPath;

    await _ampSub?.cancel();
    _ampSub = null;

    final path = await _recorder.stop();
    _isRecording = false;

    _ampController.add(0.0); // Reset amplitude

    return path ?? _outputPath;
  }

  /// Cancel without saving.
  Future<void> cancelRecording() async {
    if (!_isRecording) return;
    await _ampSub?.cancel();
    _ampSub = null;
    await _recorder.stop();
    _isRecording = false;
    _outputPath = null;
    _ampController.add(0.0);
  }
}
