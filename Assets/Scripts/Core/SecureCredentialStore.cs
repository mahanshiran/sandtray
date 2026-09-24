using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Persists authentication secrets in the platform credential store.
    /// Unsupported development platforms intentionally keep credentials in memory
    /// instead of falling back to plaintext PlayerPrefs.
    /// </summary>
    internal static class SecureCredentialStore
    {
        private const string Service = "com.mahanshiran.sandtray.auth";
        private static readonly object Sync = new object();

#if (UNITY_EDITOR && !UNITY_EDITOR_OSX && !UNITY_EDITOR_WIN) || UNITY_WEBGL || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
        private static readonly Dictionary<string, string> SessionValues = new Dictionary<string, string>();
#endif

        public static bool TryRead(string key, out string value)
        {
            value = "";
            if (string.IsNullOrEmpty(key)) return false;

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                IntPtr pointer = SandtraySecureGet(key);
                if (pointer == IntPtr.Zero) return true;
                try
                {
                    value = Marshal.PtrToStringAnsi(pointer) ?? "";
                    return true;
                }
                finally
                {
                    SandtraySecureFree(pointer);
                }
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var store = new AndroidJavaClass("com.mahanshiran.sandtray.SandtraySecureStore"))
                {
                    value = store.CallStatic<string>("get", activity, key) ?? "";
                    return true;
                }
#elif (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
                return MacKeychain.TryRead(key, out value);
#elif (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
                return WindowsCredentialManager.TryRead(key, out value);
#else
                lock (Sync)
                {
                    SessionValues.TryGetValue(key, out value);
                    value = value ?? "";
                    return true;
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SecureCredentialStore] Could not read a credential: {exception.GetType().Name}");
                value = "";
                return false;
            }
        }

        public static bool TryWrite(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (string.IsNullOrEmpty(value)) return TryDelete(key);

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return SandtraySecureSet(key, value) != 0;
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var store = new AndroidJavaClass("com.mahanshiran.sandtray.SandtraySecureStore"))
                    return store.CallStatic<bool>("set", activity, key, value);
#elif (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
                return MacKeychain.TryWrite(key, value);
#elif (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
                return WindowsCredentialManager.TryWrite(key, value);
#else
                lock (Sync)
                {
                    SessionValues[key] = value;
                    return true;
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SecureCredentialStore] Could not save a credential: {exception.GetType().Name}");
                return false;
            }
        }

        public static bool TryDelete(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return SandtraySecureDelete(key) != 0;
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var store = new AndroidJavaClass("com.mahanshiran.sandtray.SandtraySecureStore"))
                    return store.CallStatic<bool>("delete", activity, key);
#elif (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
                return MacKeychain.TryDelete(key);
#elif (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
                return WindowsCredentialManager.TryDelete(key);
#else
                lock (Sync)
                {
                    SessionValues.Remove(key);
                    return true;
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SecureCredentialStore] Could not delete a credential: {exception.GetType().Name}");
                return false;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SandtraySecureSet(string account, string value);

        [DllImport("__Internal")]
        private static extern IntPtr SandtraySecureGet(string account);

        [DllImport("__Internal")]
        private static extern int SandtraySecureDelete(string account);

        [DllImport("__Internal")]
        private static extern void SandtraySecureFree(IntPtr value);
#endif

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
        private static class MacKeychain
        {
            private const int Success = 0;
            private const int ItemNotFound = -25300;

            [DllImport("/System/Library/Frameworks/Security.framework/Security")]
            private static extern int SecKeychainFindGenericPassword(
                IntPtr keychain, uint serviceNameLength, byte[] serviceName,
                uint accountNameLength, byte[] accountName, out uint passwordLength,
                out IntPtr passwordData, out IntPtr itemRef);

            [DllImport("/System/Library/Frameworks/Security.framework/Security")]
            private static extern int SecKeychainAddGenericPassword(
                IntPtr keychain, uint serviceNameLength, byte[] serviceName,
                uint accountNameLength, byte[] accountName, uint passwordLength,
                byte[] passwordData, out IntPtr itemRef);

            [DllImport("/System/Library/Frameworks/Security.framework/Security")]
            private static extern int SecKeychainItemModifyAttributesAndData(
                IntPtr itemRef, IntPtr attributes, uint dataLength, byte[] data);

            [DllImport("/System/Library/Frameworks/Security.framework/Security")]
            private static extern int SecKeychainItemDelete(IntPtr itemRef);

            [DllImport("/System/Library/Frameworks/Security.framework/Security")]
            private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

            [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
            private static extern void CFRelease(IntPtr value);

            public static bool TryRead(string key, out string value)
            {
                value = "";
                byte[] service = Encoding.UTF8.GetBytes(Service);
                byte[] account = Encoding.UTF8.GetBytes(key);
                int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                    (uint)account.Length, account, out uint length, out IntPtr data, out IntPtr item);
                try
                {
                    if (status == ItemNotFound) return true;
                    if (status != Success) return false;
                    if (length == 0 || data == IntPtr.Zero) return true;
                    byte[] bytes = new byte[length];
                    Marshal.Copy(data, bytes, 0, bytes.Length);
                    value = Encoding.UTF8.GetString(bytes);
                    return true;
                }
                finally
                {
                    if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
                    if (item != IntPtr.Zero) CFRelease(item);
                }
            }

            public static bool TryWrite(string key, string value)
            {
                byte[] service = Encoding.UTF8.GetBytes(Service);
                byte[] account = Encoding.UTF8.GetBytes(key);
                byte[] secret = Encoding.UTF8.GetBytes(value);
                int find = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                    (uint)account.Length, account, out _, out IntPtr oldData, out IntPtr item);
                if (oldData != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, oldData);
                try
                {
                    if (find == Success)
                        return SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero,
                            (uint)secret.Length, secret) == Success;
                    if (find != ItemNotFound) return false;
                    int add = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                        (uint)account.Length, account, (uint)secret.Length, secret, out IntPtr addedItem);
                    if (addedItem != IntPtr.Zero) CFRelease(addedItem);
                    return add == Success;
                }
                finally
                {
                    if (item != IntPtr.Zero) CFRelease(item);
                }
            }

            public static bool TryDelete(string key)
            {
                byte[] service = Encoding.UTF8.GetBytes(Service);
                byte[] account = Encoding.UTF8.GetBytes(key);
                int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service,
                    (uint)account.Length, account, out _, out IntPtr data, out IntPtr item);
                if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
                try
                {
                    if (status == ItemNotFound) return true;
                    return status == Success && SecKeychainItemDelete(item) == Success;
                }
                finally
                {
                    if (item != IntPtr.Zero) CFRelease(item);
                }
            }
        }
#endif

#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
        private static class WindowsCredentialManager
        {
            private const uint Generic = 1;
            private const uint PersistLocalMachine = 2;
            private const int ErrorNotFound = 1168;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct Credential
            {
                public uint Flags;
                public uint Type;
                public string TargetName;
                public string Comment;
                public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
                public uint CredentialBlobSize;
                public IntPtr CredentialBlob;
                public uint Persist;
                public uint AttributeCount;
                public IntPtr Attributes;
                public string TargetAlias;
                public string UserName;
            }

            [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern bool CredWrite(ref Credential credential, uint flags);

            [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

            [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern bool CredDelete(string target, uint type, uint flags);

            [DllImport("advapi32.dll")]
            private static extern void CredFree(IntPtr buffer);

            private static string Target(string key) => Service + "." + key;

            public static bool TryRead(string key, out string value)
            {
                value = "";
                if (!CredRead(Target(key), Generic, 0, out IntPtr pointer))
                    return Marshal.GetLastWin32Error() == ErrorNotFound;
                try
                {
                    var credential = Marshal.PtrToStructure<Credential>(pointer);
                    if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                        return true;
                    byte[] bytes = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                    value = Encoding.UTF8.GetString(bytes);
                    return true;
                }
                finally
                {
                    CredFree(pointer);
                }
            }

            public static bool TryWrite(string key, string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                IntPtr blob = Marshal.AllocCoTaskMem(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, blob, bytes.Length);
                    var credential = new Credential
                    {
                        Type = Generic,
                        TargetName = Target(key),
                        CredentialBlobSize = (uint)bytes.Length,
                        CredentialBlob = blob,
                        Persist = PersistLocalMachine,
                        UserName = Service
                    };
                    return CredWrite(ref credential, 0);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(blob);
                }
            }

            public static bool TryDelete(string key)
            {
                return CredDelete(Target(key), Generic, 0) || Marshal.GetLastWin32Error() == ErrorNotFound;
            }
        }
#endif
    }
}
