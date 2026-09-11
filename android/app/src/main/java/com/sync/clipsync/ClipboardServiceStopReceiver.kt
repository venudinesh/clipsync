package com.sync.clipsync

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log

/**
 * BroadcastReceiver that handles the "Stop" action from the foreground notification.
 * Stops the ClipboardForegroundService gracefully.
 */
class ClipboardServiceStopReceiver : BroadcastReceiver() {

    companion object {
        private const val TAG = "ClipSyncStop"
    }

    override fun onReceive(context: Context, intent: Intent?) {
        Log.d(TAG, "Stop receiver triggered")
        val serviceIntent = Intent(context, ClipboardForegroundService::class.java)
        context.stopService(serviceIntent)
    }
}
