import 'package:flutter/material.dart';

import 'hello_screen.dart';
import 'onboarding_flow.dart';

/// The handwritten greeting, then the tutorial. The greeting is stacked on top
/// so swiping it away uncovers the tutorial underneath rather than cutting to
/// it.
class HelloFlow extends StatefulWidget {
  const HelloFlow({super.key, required this.onComplete});

  /// Called once the tutorial behind the greeting has been finished or skipped.
  final VoidCallback onComplete;

  @override
  State<HelloFlow> createState() => _HelloFlowState();
}

class _HelloFlowState extends State<HelloFlow> {
  bool _greeted = false;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: <Widget>[
        // Mounted from the start so the greeting lifts off something already
        // there, but its tickers are held until the greeting is gone — the
        // first page inks itself on when it is uncovered, not behind the
        // greeting where nobody would see it.
        Positioned.fill(
          child: TickerMode(
            enabled: _greeted,
            child: OnboardingFlow(onComplete: widget.onComplete),
          ),
        ),
        if (!_greeted)
          Positioned.fill(
            child: HelloScreen(
              onDismiss: () => setState(() => _greeted = true),
            ),
          ),
      ],
    );
  }
}
