package com.sync.clipsync

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.util.Log
import androidx.core.app.NotificationCompat
import io.flutter.embedding.engine.FlutterEngineCache
import io.flutter.plugin.common.MethodChannel

/**
 * Native Android ForegroundService that monitors the system clipboard
 * via ClipboardManager.OnPrimaryClipChangedListener.
 *
 * Features:
 * - Persistent foreground notification (Android 8+ requirement)
 * - 2-second debounce filter to prevent recursive clipboard loops
 * - MethodChannel bridge back to Flutter for processing
 * - Stores last-known clip text to detect genuine new copies
 */
class ClipboardForegroundService : Service() {

    companion object {
        private const val TAG = "ClipSyncService"
        private const val CHANNEL_ID = "clipSync_clipboard_service"
        private const val NOTIFICATION_ID = 9001
        private const val DEBOUNCE_MS = 2000L
        private const val METHOD_CHANNEL_NAME = "com.sync.clipsync/clipboard"

        // Service state
        var isRunning = false
            private set
        var lastClipboardText: String = ""
            private set

        // Flutter MethodChannel reference (set from MainActivity)
        var flutterMethodChannel: MethodChannel? = null
    }

    private lateinit var clipboardManager: ClipboardManager
    private val handler = Handler(Looper.getMainLooper())
    private var lastClipText: String = ""
    private var lastClipTimestamp: Long = 0L
    private var lastNotifiedText: String = ""

    // ── Clipboard Listener ──────────────────────────────────────────────
    private val clipboardListener = ClipboardManager.OnPrimaryClipChangedListener {
        onClipboardChanged()
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "ClipboardForegroundService created")
        clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        Log.d(TAG, "ClipboardForegroundService started")

        // Create notification channel (Android 8+)
        createNotificationChannel()

        // Start as foreground with persistent notification
        val notification = buildNotification("Monitoring clipboard for changes…")
        startForeground(NOTIFICATION_ID, notification)

        // Register clipboard listener
        registerClipboardListener()

        // Load last-known clip text to avoid re-processing on restart
        loadLastClipText()

        isRunning = true

        // Return STICKY so the service restarts if killed by the system
        return START_STICKY
    }

    override fun onDestroy() {
        Log.d(TAG, "ClipboardForegroundService destroyed")
        unregisterClipboardListener()
        isRunning = false
        super.onDestroy()
    }

    // ── Clipboard Listener Registration ─────────────────────────────────

    private fun registerClipboardListener() {
        try {
            clipboardManager.addPrimaryClipChangedListener(clipboardListener)
            Log.d(TAG, "Clipboard listener registered")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to register clipboard listener", e)
        }
    }

    private fun unregisterClipboardListener() {
        try {
            clipboardManager.removePrimaryClipChangedListener(clipboardListener)
            Log.d(TAG, "Clipboard listener unregistered")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to unregister clipboard listener", e)
        }
    }

    // ── Clipboard Change Handling ───────────────────────────────────────

    private fun onClipboardChanged() {
        val now = System.currentTimeMillis()
        val clip = clipboardManager.primaryClip ?: return

        if (clip.itemCount == 0) return

        val item = clip.getItemAt(0)
        val text = item.text?.toString() ?: return

        if (text.isBlank()) return

        // ── Debounce filter ──────────────────────────────────────────
        // Prevent recursive loops: if we recently processed the same text, skip
        val timeSinceLastClip = now - lastClipTimestamp
        if (text == lastClipText && timeSinceLastClip < DEBOUNCE_MS) {
            Log.d(TAG, "Debounce: skipping duplicate clip within ${timeSinceLastClip}ms")
            return
        }

        // Prevent notifying the same text twice in a row
        if (text == lastNotifiedText && timeSinceLastClip < DEBOUNCE_MS) {
            Log.d(TAG, "Debounce: skipping duplicate notification")
            return
        }

        // ── First-seen check ─────────────────────────────────────────
        // On service restart, we preload lastClipText; skip that initial fire
        if (text == lastClipText) {
            Log.d(TAG, "Skipping initial clip match (preload)")
            lastNotifiedText = text
            return
        }

        // ── Valid new clipboard entry ────────────────────────────────
        Log.d(TAG, "New clipboard text detected (${text.length} chars): ${text.take(80)}…")

        lastClipText = text
        lastClipTimestamp = now
        lastNotifiedText = text
        lastClipboardText = text

        // Send to Flutter for processing via MethodChannel
        sendToFlutter(text)

        // Update notification with truncated preview
        val preview = if (text.length > 60) "${text.take(60)}…" else text
        updateNotification("Copied: $preview")
    }

    private fun loadLastClipText() {
        try {
            val clip = clipboardManager.primaryClip
            if (clip != null && clip.itemCount > 0) {
                lastClipText = clip.getItemAt(0).text?.toString() ?: ""
                lastClipTimestamp = System.currentTimeMillis()
                lastClipboardText = lastClipText
                Log.d(TAG, "Preloaded last clip: ${lastClipText.take(60)}")
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to preload clipboard", e)
        }
    }

    // ── Flutter Communication ───────────────────────────────────────────

    private fun sendToFlutter(text: String) {
        val channel = flutterMethodChannel
        if (channel == null) {
            Log.w(TAG, "MethodChannel not available, queuing clip for later")
            // Try to get it from cached FlutterEngine
            tryToAttachChannel()
            return
        }

        try {
            handler.post {
                channel.invokeMethod("onClipboardChanged", mapOf("text" to text))
            }
            Log.d(TAG, "Sent clipboard text to Flutter (${text.length} chars)")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to send to Flutter", e)
        }
    }

    private fun tryToAttachChannel() {
        try {
            val engine = FlutterEngineCache.getInstance().get("main_engine")
            if (engine != null) {
                flutterMethodChannel = MethodChannel(
                    engine.dartExecutor.binaryMessenger,
                    METHOD_CHANNEL_NAME
                )
                // Retry sending
                if (lastClipboardText.isNotEmpty()) {
                    sendToFlutter(lastClipboardText)
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to attach to Flutter engine", e)
        }
    }

    // ── Notification Channel & Notification ─────────────────────────────

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                "Clipboard Monitoring",
                NotificationManager.IMPORTANCE_LOW  // Low = no sound, visible in tray
            ).apply {
                description = "Persistent notification for background clipboard monitoring"
                setShowBadge(false)
                enableVibration(false)
                setSound(null, null)
            }

            val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            nm.createNotificationChannel(channel)
            Log.d(TAG, "Notification channel created")
        }
    }

    private fun buildNotification(text: String): Notification {
        // Tap notification → open the app
        val intent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val pendingIntent = PendingIntent.getActivity(
            this, 0, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        // Stop action
        val stopIntent = Intent(this, ClipboardServiceStopReceiver::class.java)
        val stopPendingIntent = PendingIntent.getBroadcast(
            this, 1, stopIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("ClipSync AI")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_menu_save)
            .setOngoing(true)
            .setContentIntent(pendingIntent)
            .addAction(android.R.drawable.ic_media_pause, "Stop", stopPendingIntent)
            .setSilent(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .build()
    }

    private fun updateNotification(text: String) {
        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        nm.notify(NOTIFICATION_ID, buildNotification(text))
    }
}
