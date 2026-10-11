package com.orangepcsim.saveopen;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.provider.OpenableColumns;
import android.util.Log;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.Locale;

/** Copies only supported saves from Android ACTION_VIEW into the app cache. */
public final class OrangeSaveOpen {
    /** Returns displayName + '\n' + readable cache path; consumes each VIEW once. */
    public static String takeIncomingSave(Activity activity) {
        Intent intent = activity.getIntent();
        if (intent == null || !Intent.ACTION_VIEW.equals(intent.getAction())) return null;
        // UnityPlayerActivity remains the sole launcher; Android's default
        // onNewIntent updates getIntent(). Consume it before copying the file.
        activity.setIntent(new Intent(Intent.ACTION_MAIN));
        Uri uri = intent.getData();
        if (uri == null) return null;
        String name = null;
        if ("content".equals(uri.getScheme())) {
            try (Cursor cursor = activity.getContentResolver().query(uri,
                    new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null)) {
                if (cursor != null && cursor.moveToFirst()) name = cursor.getString(0);
            } catch (Exception e) { Log.w("OrangeSaveOpen", "Could not read display name", e); }
        }
        if (name == null || name.isEmpty()) name = uri.getLastPathSegment();
        if (name == null) return null;
        name = Uri.decode(name);
        name = name.substring(Math.max(name.lastIndexOf('/'), name.lastIndexOf('\\')) + 1);
        name = name.replace('\n', '_').replace('\r', '_');
        String lower = name.toLowerCase(Locale.ROOT);
        if (!lower.endsWith(".opc") && !lower.endsWith(".pc")) return null;
        String ext = lower.endsWith(".opc") ? ".opc" : ".pc";
        File tmp = null;
        try {
            tmp = File.createTempFile("incoming-save-", ext, activity.getCacheDir());
            try (InputStream input = activity.getContentResolver().openInputStream(uri);
                 FileOutputStream output = new FileOutputStream(tmp)) {
                if (input == null) throw new java.io.IOException("Cannot open save URI");
                byte[] buffer = new byte[32768];
                int read;
                long total = 0;
                while ((read = input.read(buffer)) != -1) {
                    total += read;
                    if (total > 64L * 1024L * 1024L) throw new java.io.IOException("Save too large");
                    output.write(buffer, 0, read);
                }
            }
            return name + "\n" + tmp.getAbsolutePath();
        } catch (Exception e) {
            Log.e("OrangeSaveOpen", "Could not import selected save", e);
            if (tmp != null) tmp.delete();
            return null;
        }
    }
}
