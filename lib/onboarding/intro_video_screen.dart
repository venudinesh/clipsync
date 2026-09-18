import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

/// The launch clip, shown once — right after the handwritten greeting and
/// before the tutorial. It plays the phone cut in portrait and the desktop cut
/// in landscape, full-bleed on the same pitch black the rest of the flow uses,
/// and can be skipped at any time.
///
/// If the platform has no video backend (the desktop Flutter builds) or the
/// clip fails to load, it steps aside immediately so the flow is never blocked.
class IntroVideoScreen extends StatefulWidget {
  const IntroVideoScreen({super.key, required this.onDone});

  /// Called once the clip has finished, been skipped, or could not play.
  final VoidCallback onDone;

  @override
  State<IntroVideoScreen> createState() => _IntroVideoScreenState();
}

class _IntroVideoScreenState extends State<IntroVideoScreen> {
  VideoPlayerController? _controller;
  bool _ready = false;
  bool _done = false;

  @override
  void initState() {
    super.initState();
    // Deferred so the orientation is known and we can pick the matching cut.
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<void> _load() async {
    if (!mounted) return;
    final bool portrait =
        MediaQuery.of(context).orientation == Orientation.portrait;
    final String asset = portrait
        ? 'assets/video/intro_phone.mp4'
        : 'assets/video/intro_pc.mp4';
    final VideoPlayerController controller =
        VideoPlayerController.asset(asset);
    _controller = controller;
    try {
      await controller.initialize();
      controller.addListener(_tick);
      await controller.setVolume(1);
      await controller.play();
      if (!mounted) return;
      setState(() => _ready = true);
    } catch (_) {
      // No video backend on this platform, or the asset failed to open —
      // don't strand the user on a black screen, just move on.
      _finish();
    }
  }

  void _tick() {
    final VideoPlayerController? controller = _controller;
    if (controller == null || _done) return;
    final VideoPlayerValue value = controller.value;
    final bool ended = value.isInitialized &&
        value.duration > Duration.zero &&
        value.position >= value.duration &&
        !value.isPlaying;
    if (ended) _finish();
  }

  void _finish() {
    if (_done) return;
    _done = true;
    _controller?.removeListener(_tick);
    widget.onDone();
  }

  @override
  void dispose() {
    _controller?.removeListener(_tick);
    _controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final VideoPlayerController? controller = _controller;
    return ColoredBox(
      color: const Color(0xFF0B0B0D),
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          if (_ready && controller != null)
            FittedBox(
              fit: BoxFit.cover,
              clipBehavior: Clip.hardEdge,
              child: SizedBox(
                width: controller.value.size.width,
                height: controller.value.size.height,
                child: VideoPlayer(controller),
              ),
            ),
          Positioned(
            top: 0,
            right: 0,
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: _SkipButton(onTap: _finish),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Understated skip affordance — off-white on a faint scrim so it reads over
/// the clip without borrowing the accent, which the flow keeps for the
/// tutorial's final action.
class _SkipButton extends StatelessWidget {
  const _SkipButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.black.withValues(alpha: 0.32),
      shape: const StadiumBorder(),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: const Padding(
          padding: EdgeInsets.fromLTRB(18, 10, 12, 10),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                'Skip',
                style: TextStyle(
                  color: Color(0xFFEDECEA),
                  fontSize: 15,
                  fontWeight: FontWeight.w500,
                  letterSpacing: 0.3,
                ),
              ),
              SizedBox(width: 4),
              Icon(
                Icons.chevron_right_rounded,
                size: 20,
                color: Color(0xFFEDECEA),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
