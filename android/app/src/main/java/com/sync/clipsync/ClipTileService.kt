package com.sync.clipsync

import android.content.Intent
import android.os.Build
import android.service.quicksettings.TileService
import androidx.annotation.RequiresApi

/**
 * Quick Settings tile: one tap files whatever is on the clipboard.
 *
 * The tap launches MainActivity with the CAPTURE action (unlocking first when
 * the device is locked); the activity parks the request in shared prefs and
 * Flutter picks it up on resume, so the tile needs no engine of its own.
 */
@RequiresApi(Build.VERSION_CODES.N)
class ClipTileService : TileService() {

    override fun onClick() {
        super.onClick()
        unlockAndRun {
            val intent = Intent(this, MainActivity::class.java).apply {
                action = MainActivity.TILE_CAPTURE_ACTION
                addFlags(
                    Intent.FLAG_ACTIVITY_NEW_TASK or
                        Intent.FLAG_ACTIVITY_SINGLE_TOP or
                        Intent.FLAG_ACTIVITY_CLEAR_TOP
                )
            }
            startActivityAndCollapse(intent)
        }
    }
}
