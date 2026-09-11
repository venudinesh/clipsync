import 'package:flutter/material.dart';
import 'package:hive_flutter/hive_flutter.dart';

import 'hello_flow.dart';

/// First-launch gate. Shows the handwritten greeting, then the tutorial flow,
/// then hands off to the app. Persists completion so it only appears on
/// install / data reset — never on every open.
class OnboardingGate extends StatelessWidget {
  const OnboardingGate({super.key});

  void _flowDone(BuildContext context) {
    // Persist completion so onboarding never shows again on normal launches.
    Hive.box('settings').put('onboardingCompleted', true);
    // Push the real app on top (root route).
    Navigator.of(context).pushReplacementNamed('app');
  }

  @override
  Widget build(BuildContext context) {
    return HelloFlow(onComplete: () => _flowDone(context));
  }
}
