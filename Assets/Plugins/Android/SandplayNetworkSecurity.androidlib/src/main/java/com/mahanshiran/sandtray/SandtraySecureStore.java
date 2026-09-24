package com.mahanshiran.sandtray;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

public final class SandtraySecureStore {
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String KEY_ALIAS = "com.mahanshiran.sandtray.auth.key";
    private static final String PREFS = "sandtray_secure_credentials";
    private static final byte VERSION = 1;
    private static final int IV_LENGTH = 12;

    private SandtraySecureStore() {}

    private static SharedPreferences preferences(Context context) {
        return context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    private static synchronized SecretKey secretKey() throws Exception {
        KeyStore store = KeyStore.getInstance(KEYSTORE);
        store.load(null);
        if (store.containsAlias(KEY_ALIAS))
            return (SecretKey) store.getKey(KEY_ALIAS, null);

        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE);
        generator.init(new KeyGenParameterSpec.Builder(
                KEY_ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .build());
        return generator.generateKey();
    }

    public static String get(Context context, String name) throws Exception {
        String encoded = preferences(context).getString(name, null);
        if (encoded == null || encoded.isEmpty()) return null;

        byte[] payload = Base64.decode(encoded, Base64.NO_WRAP);
        if (payload.length <= 1 + IV_LENGTH || payload[0] != VERSION)
            throw new IllegalStateException("Unsupported credential payload");

        byte[] iv = new byte[IV_LENGTH];
        byte[] ciphertext = new byte[payload.length - 1 - IV_LENGTH];
        System.arraycopy(payload, 1, iv, 0, IV_LENGTH);
        System.arraycopy(payload, 1 + IV_LENGTH, ciphertext, 0, ciphertext.length);

        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.DECRYPT_MODE, secretKey(), new GCMParameterSpec(128, iv));
        return new String(cipher.doFinal(ciphertext), StandardCharsets.UTF_8);
    }

    public static boolean set(Context context, String name, String value) throws Exception {
        if (value == null || value.isEmpty()) return delete(context, name);

        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.ENCRYPT_MODE, secretKey());
        byte[] iv = cipher.getIV();
        byte[] ciphertext = cipher.doFinal(value.getBytes(StandardCharsets.UTF_8));
        byte[] payload = ByteBuffer.allocate(1 + iv.length + ciphertext.length)
                .put(VERSION).put(iv).put(ciphertext).array();
        return preferences(context).edit().putString(name, Base64.encodeToString(payload, Base64.NO_WRAP)).commit();
    }

    public static boolean delete(Context context, String name) {
        return preferences(context).edit().remove(name).commit();
    }
}
