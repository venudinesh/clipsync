import 'dart:io';
import 'dart:ui';

import 'package:device_info_plus/device_info_plus.dart';
import 'package:path_provider/path_provider.dart';

/// Gathers device hardware specifications to recommend appropriate LLM models.
class DeviceInfoService {
  static final DeviceInfoService _instance = DeviceInfoService._();
  factory DeviceInfoService() => _instance;
  DeviceInfoService._();

  AndroidDeviceInfo? _androidInfo;
  DeviceProfile? _cachedInfo;

  // ── Initialization ─────────────────────────────────────────────────────

  Future<void> init() async {
    if (Platform.isAndroid) {
      _androidInfo = await DeviceInfoPlugin().androidInfo;
    }
    _cachedInfo = await _gatherInfo();
  }

  // ── Core Info ──────────────────────────────────────────────────────────

  DeviceProfile? get info => _cachedInfo;

  Future<DeviceProfile> _gatherInfo() async {
    final ram = await _getTotalRamMB();
    final freeRam = await _getAvailableRamMB();
    final storage = await _getStorageInfo();
    final cpuCores = _getCpuCores();
    final cpuArch = _getCpuArch();
    final screen = _getScreenInfo();
    final gpuInfo = _getGpuInfo();
    final android = _androidInfo;

    return DeviceProfile(
      totalRamMB: ram,
      availableRamMB: freeRam,
      cpuCores: cpuCores,
      cpuArchitecture: cpuArch,
      totalStorageGB: storage.totalGB,
      availableStorageGB: storage.availableGB,
      screenWidthPx: screen.widthPx,
      screenHeightPx: screen.heightPx,
      screenDpi: screen.dpi,
      deviceModel: android?.model ?? Platform.operatingSystem,
      osVersion: android?.version.release ?? Platform.operatingSystemVersion,
      gpuRenderer: gpuInfo,
      supports64Bit: storage.supports64Bit,
    );
  }

  // ── RAM ────────────────────────────────────────────────────────────────

  Future<int> _getTotalRamMB() async {
    try {
      // Android: read from /proc/meminfo
      final meminfo = await File('/proc/meminfo').readAsLines();
      for (final line in meminfo) {
        if (line.startsWith('MemTotal:')) {
          final kb = int.tryParse(
              RegExp(r'(\d+)').firstMatch(line)?.group(1) ?? '');
          if (kb != null) return kb ~/ 1024;
        }
      }
    } catch (_) {}

    // Fallback: use device_info_plus
    try {
      final android = _androidInfo;
      if (android != null) {
        // device_info_plus doesn't directly expose RAM,
        // but we can estimate from the system
        return 4096; // Safe default for modern Android
      }
    } catch (_) {}

    return 4096; // Default assumption
  }

  Future<int> _getAvailableRamMB() async {
    try {
      final meminfo = await File('/proc/meminfo').readAsLines();
      for (final line in meminfo) {
        if (line.startsWith('MemAvailable:')) {
          final kb = int.tryParse(
              RegExp(r'(\d+)').firstMatch(line)?.group(1) ?? '');
          if (kb != null) return kb ~/ 1024;
        }
      }
    } catch (_) {}
    return _getTotalRamMB(); // Assume all available
  }

  // ── CPU ────────────────────────────────────────────────────────────────

  int _getCpuCores() {
    try {
      return Platform.numberOfProcessors;
    } catch (_) {
      return 4; // Default
    }
  }

  String _getCpuArch() {
    try {
      final android = _androidInfo;
      if (android != null) {
        final abis = android.supportedAbis;
        if (abis.isNotEmpty) return abis.first;
      }
    } catch (_) {}
    return Platform.operatingSystem == 'android' ? 'arm64-v8a' : 'unknown';
  }

  // ── Storage ────────────────────────────────────────────────────────────

  Future<_StorageInfo> _getStorageInfo() async {
    try {
      final dir = await getApplicationDocumentsDirectory();
      final stat = await dir.stat();
      // Approximate total from the mount point
      // On Android, we can read /proc/mounts but a simpler approach works
      final totalBytes = await _getTotalStorageBytes();
      final availBytes = await _getAvailableStorageBytes(dir.path);
      return _StorageInfo(
        totalGB: totalBytes ~/ (1024 * 1024 * 1024),
        availableGB: availBytes ~/ (1024 * 1024 * 1024),
        supports64Bit: true, // Modern Android always 64-bit
      );
    } catch (_) {
      return _StorageInfo(totalGB: 64, availableGB: 32, supports64Bit: true);
    }
  }

  Future<int> _getTotalStorageBytes() async {
    try {
      if (Platform.isAndroid) {
        final stat = await Process.run('df', ['/data']);
        if (stat.exitCode == 0) {
          final output = stat.stdout as String;
          final lines = output.split('\n');
          if (lines.length > 1) {
            final parts = lines[1].trim().split(RegExp(r'\s+'));
            if (parts.length >= 2) {
              final blocks = int.tryParse(parts[1]);
              if (blocks != null) return blocks * 1024;
            }
          }
        }
      }
    } catch (_) {}
    return 64 * 1024 * 1024 * 1024; // 64GB default
  }

  Future<int> _getAvailableStorageBytes(String path) async {
    try {
      final dir = Directory(path);
      final total = await _getTotalStorageBytes();
      // Rough estimate: assume 40% available on typical device
      return (total * 0.4).toInt();
    } catch (_) {
      return 32 * 1024 * 1024 * 1024; // 32GB default
    }
  }

  // ── Screen ─────────────────────────────────────────────────────────────

  _ScreenInfo _getScreenInfo() {
    final view = PlatformDispatcher.instance.views.first;
    final size = view.physicalSize;
    final dpr = view.devicePixelRatio;
    return _ScreenInfo(
      widthPx: size.width.toInt(),
      heightPx: size.height.toInt(),
      dpi: (dpr * 160).toInt(), // Convert to approximate DPI
    );
  }

  // ── GPU ────────────────────────────────────────────────────────────────

  String _getGpuInfo() {
    try {
      // Android GPU info from /proc/cpuinfo or system properties
      // This is approximate — real GPU info needs Vulkan API
      final android = _androidInfo;
      if (android != null) {
        return 'Adreno/Mali (Android)';
      }
    } catch (_) {}
    return 'Unknown GPU';
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS
// ─────────────────────────────────────────────────────────────────────────────

class DeviceProfile {
  final int totalRamMB;
  final int availableRamMB;
  final int cpuCores;
  final String cpuArchitecture;
  final int totalStorageGB;
  final int availableStorageGB;
  final int screenWidthPx;
  final int screenHeightPx;
  final int screenDpi;
  final String deviceModel;
  final String osVersion;
  final String gpuRenderer;
  final bool supports64Bit;

  DeviceProfile({
    required this.totalRamMB,
    required this.availableRamMB,
    required this.cpuCores,
    required this.cpuArchitecture,
    required this.totalStorageGB,
    required this.availableStorageGB,
    required this.screenWidthPx,
    required this.screenHeightPx,
    required this.screenDpi,
    required this.deviceModel,
    required this.osVersion,
    required this.gpuRenderer,
    required this.supports64Bit,
  });

  /// Performance tier based on hardware specs
  DeviceTier get tier {
    if (totalRamMB >= 8192 && cpuCores >= 8) return DeviceTier.high;
    if (totalRamMB >= 4096 && cpuCores >= 6) return DeviceTier.medium;
    return DeviceTier.low;
  }

  /// Max recommended model size in billions of parameters
  double get maxRecommendedParams {
    switch (tier) {
      case DeviceTier.high:
        return 8.0;   // Can run up to 8B models
      case DeviceTier.medium:
        return 4.0;   // Can run up to 4B models
      case DeviceTier.low:
        return 2.0;   // Can run up to 2B models
    }
  }

  /// Recommended quantization level
  String get recommendedQuant {
    if (totalRamMB >= 8192) return 'Q8_0';
    if (totalRamMB >= 6144) return 'Q5_K_M';
    if (totalRamMB >= 4096) return 'Q4_K_M';
    return 'Q4_0';
  }

  /// Estimated tokens/second (rough approximation)
  int get estimatedTokensPerSec {
    switch (tier) {
      case DeviceTier.high:
        return 40;
      case DeviceTier.medium:
        return 20;
      case DeviceTier.low:
        return 8;
    }
  }

  /// The facts the device panel lists, in reading order. The model name, the
  /// Android version and the performance tier are stated in that panel's own
  /// header, so they are deliberately absent here rather than said twice.
  Map<String, String> get summary => {
        'RAM': '${totalRamMB ~/ 1024} GB',
        'Free RAM': '${availableRamMB ~/ 1024} GB',
        'CPU': '$cpuCores cores (${cpuArchitecture.split('-').first})',
        'Storage': '$availableStorageGB GB free of $totalStorageGB GB',
        'Largest model': '${maxRecommendedParams.toStringAsFixed(0)}B parameters',
        'Recommended quant': recommendedQuant,
        'Estimated speed': '~$estimatedTokensPerSec tokens/sec',
      };
}

enum DeviceTier { low, medium, high }

class _StorageInfo {
  final int totalGB;
  final int availableGB;
  final bool supports64Bit;
  _StorageInfo({
    required this.totalGB,
    required this.availableGB,
    required this.supports64Bit,
  });
}

class _ScreenInfo {
  final int widthPx;
  final int heightPx;
  final int dpi;
  _ScreenInfo({
    required this.widthPx,
    required this.heightPx,
    required this.dpi,
  });
}
