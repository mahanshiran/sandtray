using UnityEngine;

namespace Sandplay.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Sandplay/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Sandbox")]
        public float SandboxWidth = 10f;
        public float SandboxDepth = 10f;
        public float SandMaxHeight = 2f;
        public float SandBaseHeight = 0.5f;
        public int HeightmapResolution = 128;

        [Header("Sand Tools")]
        public float DefaultBrushRadius = 1f;
        public float DefaultBrushStrength = 0.5f;
        public float MinBrushRadius = 0.2f;
        public float MaxBrushRadius = 3f;

        [Header("Camera")]
        public float CameraMinDistance = 3f;
        public float CameraMaxDistance = 20f;
        public float CameraMinPitch = 10f;
        public float CameraMaxPitch = 85f;
        public float CameraOrbitSpeed = 5f;
        public float CameraPanSpeed = 0.5f;
        public float CameraZoomSpeed = 2f;
        public float CameraSmoothTime = 0.15f;
        [Tooltip("Front-facing yaw where the board intro animation stops.")]
        public float CameraIntroEndYaw = 0f;
        [Tooltip("Downward pitch where the board intro animation stops.")]
        public float CameraIntroEndPitch = 45f;
        [Tooltip("Final intro distance relative to the board's ideal fit distance.")]
        public float CameraIntroDistanceMultiplier = 1.15f;
        public float CameraIntroDuration = 1.8f;

        [Header("Objects")]
        public float ObjectMinScale = 0.2f;
        public float ObjectMaxScale = 3f;
        public float ObjectRotationSpeed = 90f;

        [Header("Undo")]
        public int MaxUndoSteps = 50;

        [Header("Agora Voice / Video")]
        [Tooltip("Agora App ID (fallback for non-logged-in users). Token-based auth is preferred when logged in. Leave blank to disable voice/video.")]
        public string AgoraAppId = "4e83cfbb48df4b298e769d0bf591f88b"; // Kept for fallback; backend now uses token authentication

        [Header("RevenueCat Subscription")]
        [Tooltip("Apple App Store API key from the RevenueCat dashboard")]
        public string RevenueCatAppleApiKey = "appl_OARoUMFKxBwoZIimzOcXlDFgZBa"; // TODO: rotate before public release
        [Tooltip("Google Play API key from the RevenueCat dashboard")]
        public string RevenueCatGoogleApiKey = "REPLACE_WITH_GOOGLE_KEY";
        [Tooltip("RevenueCat entitlement identifier (must match dashboard)")]
        public string RevenueCatEntitlementId = "premium";
    }
}
