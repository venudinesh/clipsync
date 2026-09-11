import 'package:local_auth/local_auth.dart';

/// Fingerprint / face unlock for the app lock screen, wherever the platform
/// has it (Android, macOS). Every call is guarded: on platforms without a
/// biometric implementation the answers are simply "no", and the PIN stays
/// the way in.
class BiometricService {
  BiometricService({LocalAuthentication? auth})
      : _auth = auth ?? LocalAuthentication();

  final LocalAuthentication _auth;

  /// True when the device can offer *some* local authentication.
  Future<bool> isSupported() async {
    try {
      return await _auth.canCheckBiometrics ||
          await _auth.isDeviceSupported();
    } catch (_) {
      return false;
    }
  }

  /// True when at least one biometric is enrolled and ready.
  Future<bool> hasEnrolledBiometrics() async {
    try {
      final available = await _auth.getAvailableBiometrics();
      return available.isNotEmpty;
    } catch (_) {
      return false;
    }
  }

  /// Runs the system prompt. False covers cancellation, failure and lockout —
  /// the PIN field stays right there underneath either way.
  Future<bool> authenticate() async {
    try {
      return await _auth.authenticate(
        localizedReason: 'Unlock ClipSync AI',
        biometricOnly: true,
        persistAcrossBackgrounding: true,
      );
    } catch (_) {
      return false;
    }
  }
}
