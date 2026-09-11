package com.sync.clipsync

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import androidx.core.content.ContextCompat
import io.flutter.embedding.android.FlutterFragmentActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.embedding.engine.FlutterEngineCache
import io.flutter.plugin.common.MethodChannel

/**
 * Main Flutter activity that:
 * 1. Registers the Dart ↔ Kotlin MethodChannel
 * 2. Starts/stops the native ClipboardForegroundService
 * 3. Exposes clipboard read/write operations to Flutter
 *
 * A FragmentActivity (rather than a plain FlutterActivity) because the
 * biometric prompt needs a fragment host.
 */
class MainActivity : FlutterFragmentActivity() {

    companion object {
        private const val TAG = "MainActivity"
        private const val METHOD_CHANNEL_NAME = "com.sync.clipsync/clipboard"
        private const val SERVICE_CHANNEL_NAME = "com.sync.clipsync/service"
        private const val SHARE_CHANNEL_NAME = "com.sync.clipsync/share"
        private const val SHARED_PREFS = "com.sync.clipsync.prefs"

        /** Action the Quick Settings tile launches us with. */
        const val TILE_CAPTURE_ACTION = "com.sync.clipsync.CAPTURE"
    }

    private var clipboardChannel: MethodChannel? = null
    private var serviceChannel: MethodChannel? = null
    private var shareChannel: MethodChannel? = null

    // ── Incoming Intents (share sheet, quick tile) ──────────────────────

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        handleExternalIntent(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleExternalIntent(intent)
    }

    /**
     * Parks what the system handed us in shared prefs for Flutter to collect
     * on resume. Shared text files as a clip; the tile action asks for a
     * capture. The intent is consumed so a rotation does not file it twice.
     */
    private fun handleExternalIntent(intent: Intent?) {
        if (intent == null) return
        val prefs = getSharedPreferences(SHARED_PREFS, MODE_PRIVATE)
        if (intent.action == Intent.ACTION_SEND && intent.type?.startsWith("text/") == true) {
            intent.getStringExtra(Intent.EXTRA_TEXT)?.let { text ->
                if (text.isNotBlank()) {
                    prefs.edit().putString("pending_share", text).apply()
                }
            }
            intent.action = null
        } else if (intent.action == TILE_CAPTURE_ACTION) {
            prefs.edit().putString("pending_tile", "capture").apply()
            intent.action = null
        }
    }

    // ── Flutter Engine Setup ────────────────────────────────────────────

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // Cache the engine so the service can find it
        FlutterEngineCache.getInstance().put("main_engine", flutterEngine)

        setupClipboardChannel(flutterEngine)
        setupServiceChannel(flutterEngine)
        setupShareChannel(flutterEngine)
    }

    // ── Clipboard MethodChannel ─────────────────────────────────────────

    private fun setupClipboardChannel(flutterEngine: FlutterEngine) {
        clipboardChannel = MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            METHOD_CHANNEL_NAME
        ).apply {
            setMethodCallHandler { call, result ->
                when (call.method) {
                    "readClipboard" -> {
                        result.success(readClipboardText())
                    }
                    "writeClipboard" -> {
                        val text = call.argument<String>("text") ?: ""
                        writeClipboardText(text)
                        result.success(true)
                    }
                    else -> result.notImplemented()
                }
            }
        }

        // Expose to the service
        ClipboardForegroundService.flutterMethodChannel = clipboardChannel
    }

    // ── Service Control MethodChannel ───────────────────────────────────

    private fun setupServiceChannel(flutterEngine: FlutterEngine) {
        serviceChannel = MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            SERVICE_CHANNEL_NAME
        ).apply {
            setMethodCallHandler { call, result ->
                when (call.method) {
                    "startService" -> {
                        startClipboardService()
                        result.success(true)
                    }
                    "stopService" -> {
                        stopClipboardService()
                        result.success(true)
                    }
                    "isServiceRunning" -> {
                        result.success(ClipboardForegroundService.isRunning)
                    }
                    "getLastClipboardText" -> {
                        result.success(ClipboardForegroundService.lastClipboardText)
                    }
                    "requestNotificationPermission" -> {
                        requestNotificationPermission()
                        result.success(true)
                    }
                    else -> result.notImplemented()
                }
            }
        }
    }

    // ── Share / Tile MethodChannel ──────────────────────────────────────

    private fun setupShareChannel(flutterEngine: FlutterEngine) {
        shareChannel = MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            SHARE_CHANNEL_NAME
        ).apply {
            setMethodCallHandler { call, result ->
                val prefs = getSharedPreferences(SHARED_PREFS, MODE_PRIVATE)
                when (call.method) {
                    "getSharedText" -> {
                        result.success(prefs.getString("pending_share", null))
                    }
                    "clearSharedText" -> {
                        prefs.edit().remove("pending_share").apply()
                        result.success(true)
                    }
                    "getTileAction" -> {
                        result.success(prefs.getString("pending_tile", null))
                    }
                    "clearTileAction" -> {
                        prefs.edit().remove("pending_tile").apply()
                        result.success(true)
                    }
                    else -> result.notImplemented()
                }
            }
        }
    }

    // ── Clipboard Operations ────────────────────────────────────────────

    private fun readClipboardText(): String? {
        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        val clip = cm.primaryClip
        if (clip != null && clip.itemCount > 0) {
            return clip.getItemAt(0).text?.toString()
        }
        return null
    }

    private fun writeClipboardText(text: String) {
        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        val clipData = ClipData.newPlainText("ClipSync", text)
        cm.setPrimaryClip(clipData)

        // Update the service's last-known text to prevent re-processing the write
        ClipboardForegroundService.lastClipboardText.let {
            // The service will see this change; it debounces by comparing text
        }
    }

    // ── Service Control ─────────────────────────────────────────────────

    private fun startClipboardService() {
        val intent = Intent(this, ClipboardForegroundService::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            startForegroundService(intent)
        } else {
            startService(intent)
        }
    }

    private fun stopClipboardService() {
        val intent = Intent(this, ClipboardForegroundService::class.java)
        stopService(intent)
    }

    // ── Permissions ─────────────────────────────────────────────────────

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            // Android 13+ requires POST_NOTIFICATIONS runtime permission.
            // Never re-prompt if already granted, or if the user has already
            // been asked once (granted or permanently denied) — otherwise the
            // system permission dialog would cover the app on every launch,
            // leaving the screen blank/black until the user dismisses it.
            val granted = ContextCompat.checkSelfPermission(
                this,
                android.Manifest.permission.POST_NOTIFICATIONS
            ) == PackageManager.PERMISSION_GRANTED
            if (granted) return
            if (
                !shouldShowRequestPermissionRationale(
                    android.Manifest.permission.POST_NOTIFICATIONS
                ) && hasAskedNotificationPermission()
            ) {
                return
            }
            markNotificationAsked()
            requestPermissions(
                arrayOf(android.Manifest.permission.POST_NOTIFICATIONS),
                1001
            )
        }
    }

    /** Tracks whether the user has ever been shown the POST_NOTIFICATIONS prompt. */
    private fun hasAskedNotificationPermission(): Boolean =
        getSharedPreferences(SHARED_PREFS, MODE_PRIVATE)
            .getBoolean("notification_permission_asked", false)

    private fun markNotificationAsked() {
        getSharedPreferences(SHARED_PREFS, MODE_PRIVATE)
            .edit()
            .putBoolean("notification_permission_asked", true)
            .apply()
    }

    // ── Cleanup ─────────────────────────────────────────────────────────

    override fun onDestroy() {
        clipboardChannel?.setMethodCallHandler(null)
        serviceChannel?.setMethodCallHandler(null)
        shareChannel?.setMethodCallHandler(null)
        super.onDestroy()
    }
}
