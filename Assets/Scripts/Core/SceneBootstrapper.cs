using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.TextCore.LowLevel;
using Sandplay.Core;
using Sandplay.Sand;
using Sandplay.Objects;
using Sandplay.Data;
using Sandplay.AI;
using Sandplay.Camera;
using Sandplay.UI;

namespace Sandplay.Core
{
    /// <summary>
    /// Bootstraps the entire sandbox scene from code. Add this to an empty GameObject in the scene.
    /// Assign a GameConfig and ObjectCatalog asset via the inspector.
    /// </summary>
    /// <remarks>
    /// This class is intentionally split across several partial files for maintainability:
    ///   • SceneBootstrapper.cs           – core lifecycle, fields, sandbox/UI bootstrap
    ///   • SceneBootstrapper.Agora.cs     – Agora video-call panel + tile management
    ///   • SceneBootstrapper.PatientPointer.cs – Therapist-mode patient cursor sharing UI
    /// All partial files share fields and helpers; nothing changes about access or behavior.
    /// </remarks>
    public partial class SceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private ObjectCatalog _catalog;

        [Header("Optional: Custom Sand Textures")]
        [Tooltip("Optional: Assign a custom sand texture (e.g., from Yughues Free Sand Materials). Leave empty to use procedurally generated texture.")]
        [SerializeField] private Texture2D _customSandTexture;
        [Tooltip("Optional: Assign a custom sand normal map. Leave empty to use procedurally generated normal map.")]
        [SerializeField] private Texture2D _customSandNormalMap;

        private bool _initialized = false;
        private string _currentBoardName;
        private GameObject _sandboxRoot;   // parent of all 3D sandbox objects
        private GameObject _sandboxUI;     // toolbar / catalog / status / action panel
        private GameObject _mainMenuPanel; // full-screen main menu overlay
        private GameObject _mainMenuBackground; // background image (ignores safe area)
        private Button _accountBtn;         // login/account button in main menu
        private GameObject _canvasGo;      // shared canvas
        private GameObject _safeArea;      // safe area container for notched devices
        private GameObject _nameDialogPanel; // name input dialog overlay
        private GameObject _reportsPanel;    // board reports viewer panel
        private GameObject _settingsPanel;   // settings HUD overlay
        private GameObject _catalogPanel;    // catalog drawer
        private GameObject _catalogButton;   // catalog toggle button (right edge)
        private RectTransform _catalogButtonRT;
        private Image _catalogButtonImage;
        private GameObject _catalogButtonHandleVisual;
        private GameObject _catalogButtonGlyph;
        private bool _catalogPhoneLayout;
        private Coroutine _catalogDrawerRoutine;
        private bool _catalogDrawerOpen;
        private GameObject _colorPickerDialog; // color picker dialog
        private GameObject _networkPanel;      // host/join UI overlay
        private GameObject _paywallPanel;      // subscription paywall overlay
        private Button _proBtn;                // PRO badge in main menu header
        private TextMeshProUGUI _networkStatusText;       // connected clients display
        private TextMeshProUGUI _roomCodeText;            // cloud room code display
        private readonly System.Collections.Generic.List<(TextMeshProUGUI text, string key)> _localizedTexts =
            new System.Collections.Generic.List<(TextMeshProUGUI, string)>();
        private GameObject _netOverlayGo;      // top-center network info overlay
        private Image _qrCodeImage;            // QR code for room code
        private GameObject _spectatorBadge;    // spectating indicator
        private GameObject _agoraPanelGo;       // floating communication panel
        private RectTransform _agoraPanelRT;    // resized dynamically with participant count
        private GameObject _agoraToggleGo;      // small toggle button that opens the panel
        private Transform _agoraVideoRow;      // horizontal tile container inside panel
        private TextMeshProUGUI _agoraToggleLbl;     // Show/Hide label on toggle button
        private readonly System.Collections.Generic.List<(uint uid, GameObject tile)> _agoraUserTiles =
            new System.Collections.Generic.List<(uint, GameObject)>();
        private enum HostMode { None, Cloud, LAN }
        private HostMode _pendingHostMode = HostMode.None;
        private bool _hostTherapistMode = false;

        // Network catalog
        private GameObject _catalogContentGo;
        private TextMeshProUGUI _catalogStatusText;
        private NetworkCatalogItem[] _networkCatalogItems;

        // Material references for color customization
        private Material _sandMaterial;
        private Material _wallOuterMaterial;
        private Material _wallInnerMaterial;
        private Material _floorMaterial;
        private Material _roundedUIMaterial;

        // Default colors
        private readonly Color _defaultSandColor = new Color(0.70f, 0.60f, 0.45f);
        private readonly Color _defaultWallOuterColor = new Color(157f / 255f, 151f / 255f, 53f / 255f);
        private readonly Color _defaultWallInnerColor = new Color(0.15f, 0.45f, 0.75f);
        private readonly Color _defaultFloorColor = new Color(0.15f, 0.45f, 0.75f);

        // Color row slider references for restore defaults
        private System.Collections.Generic.Dictionary<string, (Slider r, Slider g, Slider b, Image preview, System.Action<Color> callback)> _colorRows = new System.Collections.Generic.Dictionary<string, (Slider, Slider, Slider, Image, System.Action<Color>)>();

        /// <summary>
        /// Initialize with runtime-created config and catalog (for AutoBootstrap).
        /// </summary>
        public void Initialize(GameConfig config, ObjectCatalog catalog)
        {
            _config = config;
            _catalog = catalog;
            _initialized = true;
            Bootstrap();
        }

        public void UpdateMaterialReferences(SandboxFrame frame)
        {
            _wallOuterMaterial = frame.OuterWallMaterial;
            _wallInnerMaterial = frame.InnerPanelMaterial;
            _floorMaterial = frame.FloorMaterial;
        }

        internal GameConfig WorkspaceConfig => _config;
        internal Sandplay.Objects.ObjectCatalog WorkspaceCatalog => _catalog;

        private void OnDestroy()
        {
            ClearJoinedSessionLoadingHandlers();
            Localization.OnLanguageChanged -= RefreshAllLocalizedTexts;
            if (BackendClient.Instance != null)
                BackendClient.Instance.OnAccountStatusChanged -= OnAccountStatusChanged;
            UnwireNotificationHeadsUp();
            if (AgoraManager.Instance != null)
            {
                AgoraManager.Instance.OnRemoteUserJoined -= AddAgoraRemoteTile;
                AgoraManager.Instance.OnRemoteUserLeft -= RemoveAgoraRemoteTile;
                AgoraManager.Instance.OnStateChanged -= UpdateAgoraControlState;
                AgoraManager.Instance.OnTokenPrivilegeWillExpire -= RenewAgoraToken;
                AgoraManager.Instance.OnLocalVideoReady -= RefreshLocalAgoraTileVideo;
                AgoraManager.Instance.OnRemoteVideoReady -= RefreshRemoteAgoraTileVideo;
            }
        }

        private void Awake()
        {
            // Skip if already initialized programmatically
            if (_initialized)
                return;

            // If assets are assigned in inspector, bootstrap immediately
            // Otherwise, wait for Initialize() to be called programmatically
            if (_config != null && _catalog != null)
            {
                Bootstrap();
            }
        }

        private void Bootstrap()
        {
            Debug.Log("[Sandplay] Bootstrap starting...");
            Localization.AutoDetect(); // set language (PlayerPrefs > system) before any UI is built
            if (Sandplay.Data.LocalAccountStorage.RequiresRestart)
            {
                Sandplay.Data.LocalAccountStorage.ShowRestartShield();
                return;
            }
            // RevenueCatManager.IsDebugMode = true; // DISABLED FOR PRODUCTION - only enable for testing
            // Validate dependencies
            if (_config == null)
            {
                Debug.LogError("SceneBootstrapper: GameConfig is required!");
                return;
            }

            if (_catalog == null)
            {
                Debug.LogError("SceneBootstrapper: ObjectCatalog is required!");
                return;
            }

            // Create rounded corner UI material
            var roundedShader = Shader.Find("UI/RoundedCorners");
            if (roundedShader == null)
            {
                // Shader.Find may fail if shader isn't referenced — load from Resources
                var shaderAsset = Resources.Load<Shader>("Shaders/RoundedUI");
                if (shaderAsset != null) roundedShader = shaderAsset;
            }
            if (roundedShader != null)
            {
                _roundedUIMaterial = new Material(roundedShader);
                _roundedUIMaterial.SetFloat("_CornerRadius", 8f);
                Debug.Log("[Sandplay] Rounded UI material created");
            }
            else
            {
                Debug.LogWarning("[Sandplay] UI/RoundedCorners shader not found. UI will have sharp corners.");
            }

            // Group sandbox scene objects under a root
            _sandboxRoot = new GameObject("SandboxRoot");

            CreateGameManager();
            CreateSandbox();
            CreateCamera();
            CreateObjectPlacer();
            CreateSessionManager();
            CreateScreenshotManager();
            CreateAIManager();
            CreateUndoManager();
            CreateRevenueCatManager();
            BackendClient.Instance.OnAccountStatusChanged += OnAccountStatusChanged;
            CreateAgoraManager();

            try { CreateEnvironment(); }
            catch (System.Exception e) { Debug.LogWarning($"[Sandplay] CreateEnvironment failed: {e.Message}"); }

            CreateUI();

            Localization.OnLanguageChanged += RefreshAllLocalizedTexts;

            // Start with sandbox hidden, show menu
            _sandboxRoot.SetActive(false);
            _sandboxUI.SetActive(false);
            ShowMainMenu();
        }

        /// <summary>
        /// Safety fallback: if Bootstrap ran but the menu didn't show (e.g. an exception
        /// interrupted the flow), ensure we land on the menu on the next frame.
        /// </summary>
        private void Start()
        {
            if (_initialized && _mainMenuPanel == null && _canvasGo != null)
            {
                Debug.LogWarning("[Sandplay] Menu not shown after Bootstrap — retrying in Start().");
                _sandboxRoot?.SetActive(false);
                if (_sandboxUI != null) _sandboxUI.SetActive(false);
                ShowMainMenu();
            }
        }

        private void Update()
        {
            if (Sandplay.Data.LocalAccountStorage.RequiresRestart) { Sandplay.Data.LocalAccountStorage.ShowRestartShield(); return; }
            UpdateAccessPolicy();
            UpdateAccessUsage();
            UpdateHostingWallet();
            UpdateHostingWarning();
            RefreshScheduleBadgeClock();
            // Close catalog on tap outside when open
            if (_catalogPanel != null && _catalogPanel.activeSelf)
            {
                if (InputHelper.GetPointerDown())
                {
                    // Check if pointer is over the catalog panel itself
                    if (!RectTransformUtility.RectangleContainsScreenPoint(
                        _catalogPanel.GetComponent<RectTransform>(),
                        InputHelper.GetPointerPosition(),
                        null))
                    {
                        CloseCatalogPanel();
                    }
                }
            }

            // Update replay HUD time/seek bar (no-op when not replaying)
            TickReplayHUD();
        }

        private void CreateGameManager()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            gm.Initialize(_config);
        }

        private void CreateSandbox()
        {
            var go = new GameObject("Sandbox");
            go.transform.SetParent(_sandboxRoot.transform, false);

            // Sand mesh - let RequireComponent handle adding MeshFilter/MeshRenderer/MeshCollider
            var sand = go.AddComponent<SandMesh>();
            sand.Initialize(_config);

            // Sand material - use a temporary primitive to get a guaranteed working material
            var mr = go.GetComponent<MeshRenderer>();
            var tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var sandMat = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
            Destroy(tempPrimitive);
            sandMat.color = new Color(0.70f, 0.60f, 0.45f); // Brown sand
            sandMat.SetFloat("_Metallic", 0f);
            sandMat.SetFloat("_Glossiness", 0.08f); // Matte sand

            // Use custom texture if assigned, otherwise generate procedurally
            if (_customSandTexture != null)
            {
                sandMat.mainTexture = _customSandTexture;
                Debug.Log("[Sandplay] Using custom sand texture: " + _customSandTexture.name);
            }
            else
            {
                sandMat.mainTexture = GenerateSandGrainTexture(512, 512);
                Debug.Log("[Sandplay] Using procedurally generated sand texture");
            }
            sandMat.mainTextureScale = new Vector2(10f, 10f);

            // Use custom normal map if assigned, otherwise generate procedurally
            sandMat.EnableKeyword("_NORMALMAP");
            if (_customSandNormalMap != null)
            {
                sandMat.SetTexture("_BumpMap", _customSandNormalMap);
                Debug.Log("[Sandplay] Using custom sand normal map: " + _customSandNormalMap.name);
            }
            else
            {
                sandMat.SetTexture("_BumpMap", GenerateSandNormalMap(512, 512));
                Debug.Log("[Sandplay] Using procedurally generated sand normal map");
            }
            sandMat.SetFloat("_BumpScale", 0.8f);
            sandMat.SetTextureScale("_BumpMap", new Vector2(10f, 10f));

            // Detail/occlusion map for extra grain depth
            sandMat.EnableKeyword("_DETAIL_MULX2");
            sandMat.SetTexture("_DetailAlbedoMap", GenerateSandDetailTexture(256, 256));
            sandMat.SetTextureScale("_DetailAlbedoMap", new Vector2(30f, 30f));
            sandMat.SetFloat("_DetailNormalMapScale", 0.4f);
            sandMat.SetTexture("_DetailNormalMap", GenerateSandNormalMap(256, 256));
            sandMat.SetTextureScale("_DetailNormalMap", new Vector2(30f, 30f));

            mr.material = sandMat;
            mr.receiveShadows = true;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _sandMaterial = sandMat;
            Debug.Log($"[Sandplay] Sand material assigned: shader={sandMat.shader.name}, color={sandMat.color}");

            // Frame
            var frame = go.AddComponent<SandboxFrame>();
            frame.Initialize(_config);

            // Store material references (after frame is built in Start)
            StartCoroutine(StoreFrameMaterialsAfterStart(frame));

            // Sand tool controller
            var toolGo = new GameObject("SandToolController");
            var stc = toolGo.AddComponent<SandToolController>();
            stc.Initialize(_config, sand);

            // Sand material controller
            var smcGo = new GameObject("SandMaterialController");
            var smc = smcGo.AddComponent<Sandplay.Sand.SandMaterialController>();
            smc.Initialize(_sandMaterial);

            // Network sync managers
            var sandSyncGo = new GameObject("SandSyncManager");
            var sandSync = sandSyncGo.AddComponent<SandSyncManager>();
            sandSync.Initialize(sand);

            var objSyncGo = new GameObject("ObjectSyncManager");
            objSyncGo.AddComponent<ObjectSyncManager>();

            // Network bootstrapper (persistent)
            if (NetworkBootstrapper.Instance == null)
            {
                var netGo = new GameObject("NetworkBootstrapper");
                netGo.AddComponent<NetworkBootstrapper>();
            }
        }

        private void CreateCamera()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<UnityEngine.Camera>();
                go.AddComponent<AudioListener>();
            }

            var sc = cam.gameObject.GetComponent<SandboxCamera>();
            if (sc == null)
                sc = cam.gameObject.AddComponent<SandboxCamera>();

            sc.Initialize(_config);

            // Walk mode controller (on same camera GO)
            if (cam.gameObject.GetComponent<Sandplay.Camera.WalkModeController>() == null)
                cam.gameObject.AddComponent<Sandplay.Camera.WalkModeController>();

            // Position camera
            cam.transform.position = new Vector3(0, 10, -10);
            cam.transform.LookAt(Vector3.zero);
        }

        private void CreateObjectPlacer()
        {
            var go = new GameObject("ObjectPlacer");
            go.transform.SetParent(_sandboxRoot.transform, false);
            var op = go.AddComponent<ObjectPlacer>();
            op.Initialize(_catalog);
        }

        private void CreateSessionManager()
        {
            var go = new GameObject("SessionManager");
            var sm = go.AddComponent<SessionManager>();
            sm.Initialize(_catalog, FindAnyObjectByType<ObjectPlacer>(), SandMesh.Instance);
        }

        private void CreateScreenshotManager()
        {
            var go = new GameObject("ScreenshotManager");
            go.AddComponent<ScreenshotManager>();
            // Resume any paid reflection backup interrupted by an app close or network loss.
            AnalysisArchiveClient.Instance.EnsureAccount();
        }

        private void CreateAIManager()
        {
            var go = new GameObject("AIAnalysisManager");
            go.AddComponent<AIAnalysisManager>();
        }

        private void CreateUndoManager()
        {
            var go = new GameObject("UndoManager");
            go.transform.SetParent(transform); // Parent to SceneBootstrapper so it stays active
            go.AddComponent<Sandplay.Data.UndoManager>();
            Debug.Log("[SceneBootstrapper] UndoManager created and active");
        }

        private GameObject _therapyRoom;

        private void CreateEnvironment()
        {
            ApplyEnvironmentLighting();
            BuildTherapyRoom();
        }

        private void ApplyEnvironmentLighting()
        {
            // Warm trilight ambient for cozy room feel.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.70f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.85f, 0.78f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.50f, 0.45f);

            // Disable skybox — we have a room now
            RenderSettings.skybox = null;
            var camera = UnityEngine.Camera.main ?? FindAnyObjectByType<UnityEngine.Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.13f, 0.12f);
            }
            else Debug.LogWarning("[Sandplay] Environment camera is not ready; room lighting will still be created.");

            // A graphics-device/GI refresh problem must never prevent creation of
            // the actual room lights below.
            try { DynamicGI.UpdateEnvironment(); }
            catch (System.Exception error)
            { Debug.LogWarning("[Sandplay] Ambient lighting refresh skipped: " + error.GetType().Name); }
        }

        /// <summary>
        /// Builds (or rebuilds) the therapy room sized to the current board.
        /// </summary>
        private void BuildTherapyRoom()
        {
            // Destroy previous room if resizing
            if (_therapyRoom != null)
            {
                // Destroy is deferred until the end of the frame. Disable first
                // so old and replacement lights can never illuminate together.
                _therapyRoom.SetActive(false);
                Destroy(_therapyRoom);
            }

            // === Dynamic Room Dimensions ===
            // Scale room based on board size with a generous multiplier, minimum 80
            float boardW = _config != null ? _config.SandboxWidth : 10f;
            float boardD = _config != null ? _config.SandboxDepth : 10f;
            float boardMax = Mathf.Max(boardW, boardD);
            float roomWidth = Mathf.Max(80f, boardMax * 8f);   // X
            float roomDepth = Mathf.Max(80f, boardMax * 8f);   // Z
            float roomHeight = Mathf.Max(40f, boardMax * 4f);  // Y
            float wallThick = 0.15f;
            float floorY = -0.1f;

            Color wallColor = new Color(0.92f, 0.88f, 0.82f);      // warm cream/beige
            Color floorColor = new Color(0.55f, 0.40f, 0.28f);     // warm wood brown
            Color ceilingColor = new Color(0.95f, 0.93f, 0.88f);   // off-white
            Color trimColor = new Color(0.45f, 0.32f, 0.22f);      // dark wood trim

            _therapyRoom = new GameObject("TherapyRoom");
            _therapyRoom.transform.SetParent(_sandboxRoot.transform, false);
            var roomParent = _therapyRoom;

            // === Floor ===
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(roomParent.transform, false);
            floor.transform.position = new Vector3(0, floorY - wallThick / 2f, 0);
            floor.transform.localScale = new Vector3(roomWidth, wallThick, roomDepth);
            var floorMat = floor.GetComponent<Renderer>().material;
            floorMat.color = floorColor;
            floorMat.SetFloat("_Metallic", 0f);
            floorMat.SetFloat("_Glossiness", 0.6f);
            floorMat.mainTexture = GenerateWoodFloorTexture(512, 512);
            floorMat.mainTextureScale = new Vector2(8f, 8f);
            floorMat.EnableKeyword("_NORMALMAP");
            floorMat.SetTexture("_BumpMap", GenerateWoodFloorNormalMap(512, 512));
            floorMat.SetFloat("_BumpScale", 0.3f);
            floorMat.SetTextureScale("_BumpMap", new Vector2(8f, 8f));
            floor.layer = LayerMask.NameToLayer("Ignore Raycast");

            // === Ceiling ===
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(roomParent.transform, false);
            ceiling.transform.position = new Vector3(0, floorY + roomHeight, 0);
            ceiling.transform.localScale = new Vector3(roomWidth, wallThick, roomDepth);
            var ceilMat = ceiling.GetComponent<Renderer>().material;
            ceilMat.color = ceilingColor;
            ceilMat.SetFloat("_Metallic", 0f);
            ceilMat.SetFloat("_Glossiness", 0.2f);
            ceiling.layer = LayerMask.NameToLayer("Ignore Raycast");
            // CRITICAL: ceiling must NOT cast shadows or it blocks the directional light to the sandbox below
            ceiling.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // === Walls ===
            // Back wall (far +Z)
            CreateRoomWall(roomParent.transform, "WallBack",
                new Vector3(0, floorY + roomHeight / 2f, roomDepth / 2f),
                new Vector3(roomWidth, roomHeight, wallThick), wallColor);

            // Front wall (near -Z) — door on the same side as the board overview camera
            BuildFrontDoorWall(roomParent.transform, roomWidth, roomHeight, roomDepth, wallThick, floorY, wallColor, trimColor);

            // Left wall (solid)
            CreateRoomWall(roomParent.transform, "WallLeft",
                new Vector3(-roomWidth / 2f, floorY + roomHeight / 2f, 0f),
                new Vector3(wallThick, roomHeight, roomDepth), wallColor);

            // Right wall — has a window
            BuildWindowWall(roomParent.transform, roomWidth, roomHeight, roomDepth, wallThick, floorY, wallColor, trimColor);

            // === Baseboards (trim along bottom of walls) ===
            float trimH = 0.25f;
            CreateRoomWall(roomParent.transform, "TrimBack",
                new Vector3(0, floorY + trimH / 2f, roomDepth / 2f - wallThick),
                new Vector3(roomWidth - wallThick * 2, trimH, wallThick * 0.5f), trimColor);
            CreateRoomWall(roomParent.transform, "TrimLeft",
                new Vector3(-roomWidth / 2f + wallThick, floorY + trimH / 2f, 0),
                new Vector3(wallThick * 0.5f, trimH, roomDepth - wallThick * 2), trimColor);
            CreateRoomWall(roomParent.transform, "TrimRight",
                new Vector3(roomWidth / 2f - wallThick, floorY + trimH / 2f, 0),
                new Vector3(wallThick * 0.5f, trimH, roomDepth - wallThick * 2), trimColor);

            // === Ceiling Light (visible fixture + spot light pointing straight down) ===
            float ceilLightY = floorY + roomHeight - wallThick * 0.5f;

            // Visible fixture housing
            var fixtureGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fixtureGo.name = "CeilingFixture";
            fixtureGo.transform.SetParent(roomParent.transform, false);
            fixtureGo.transform.position = new Vector3(0, ceilLightY, 0);
            fixtureGo.transform.localScale = new Vector3(roomWidth * 0.05f, wallThick * 0.6f, roomWidth * 0.05f);
            var fixtureMat = fixtureGo.GetComponent<Renderer>().material;
            fixtureMat.color = new Color(0.95f, 0.93f, 0.88f);
            fixtureMat.SetFloat("_Metallic", 0.4f);
            fixtureMat.SetFloat("_Glossiness", 0.7f);
            fixtureGo.layer = LayerMask.NameToLayer("Ignore Raycast");
            fixtureGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(fixtureGo.GetComponent<Collider>());

            // Emissive glow disc on the bottom face
            var glowGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glowGo.name = "CeilingFixtureGlow";
            glowGo.transform.SetParent(roomParent.transform, false);
            glowGo.transform.position = new Vector3(0, ceilLightY - wallThick * 0.55f, 0);
            glowGo.transform.localScale = new Vector3(roomWidth * 0.048f, wallThick * 0.05f, roomWidth * 0.048f);
            var glowMat = new Material(Shader.Find("Standard"));
            glowMat.color = new Color(1f, 0.97f, 0.88f);
            glowMat.SetFloat("_Metallic", 0f);
            glowMat.SetFloat("_Glossiness", 0f);
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.75f) * 2f);
            glowGo.GetComponent<Renderer>().material = glowMat;
            glowGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowGo.layer = LayerMask.NameToLayer("Ignore Raycast");
            Destroy(glowGo.GetComponent<Collider>());

            // Spot light pointing straight down from the fixture
            var lightGo = new GameObject("RoomLight");
            lightGo.transform.SetParent(roomParent.transform, false);
            lightGo.transform.position = new Vector3(0, ceilLightY - wallThick, 0);
            lightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
            var roomLight = lightGo.AddComponent<Light>();
            roomLight.type = LightType.Spot;
            roomLight.color = new Color(1f, 0.97f, 0.88f); // warm white
            roomLight.intensity = 3f;
            roomLight.range = roomHeight * 2.5f;
            roomLight.spotAngle = 90f;
            roomLight.shadows = LightShadows.Soft;
            roomLight.shadowStrength = 0.75f;
            roomLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            roomLight.shadowBias = 0.05f;
            roomLight.shadowNormalBias = 0.4f;

            // Force project-wide shadow quality
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 50f);
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;

            // Ambient fill — low intensity point light so unlit areas aren't pitch black
            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(roomParent.transform, false);
            fillGo.transform.position = new Vector3(0, floorY + roomHeight * 0.5f, 0);
            var fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.9f, 0.92f, 1f);
            fillLight.intensity = 0.35f;
            fillLight.range = roomWidth;
            fillLight.shadows = LightShadows.None;

            // Decoration is intentionally last and isolated. A missing or bad
            // optional asset must not prevent the room lights from existing.
            try { PlaceRoomShelfAsset(roomParent.transform, roomWidth, roomHeight, roomDepth, wallThick, floorY); }
            catch (System.Exception error)
            { Debug.LogWarning("[Sandplay] Room shelf skipped: " + error.GetType().Name); }
            try { PlaceRoomWallMap(roomParent.transform, roomWidth, roomHeight, roomDepth, wallThick, floorY); }
            catch (System.Exception error)
            { Debug.LogWarning("[Sandplay] Room wall map skipped: " + error.GetType().Name); }
        }

        /// <summary>
        /// Front wall (-Z) with hinged door. Placed on the same side as the intro overview
        /// camera so entry faces the tray without a big turn.
        /// </summary>
        private void BuildFrontDoorWall(Transform parent, float roomWidth, float roomHeight, float roomDepth,
            float wallThick, float floorY, Color wallColor, Color trimColor)
        {
            float wallZ = -roomDepth / 2f;
            float wallCenterY = floorY + roomHeight / 2f;

            float doorHalfW = roomWidth * 0.07f; // half-width along X
            float doorH = roomHeight * 0.65f;
            float doorTopY = floorY + doorH;
            float doorCenterX = 0f; // centered with the board / final camera

            // Wall segments around the door opening
            float sideW = roomWidth / 2f - doorHalfW;
            CreateRoomWall(parent, "WallFront_Left",
                new Vector3(-(doorHalfW + sideW / 2f), wallCenterY, wallZ),
                new Vector3(sideW, roomHeight, wallThick), wallColor);
            CreateRoomWall(parent, "WallFront_Right",
                new Vector3(doorHalfW + sideW / 2f, wallCenterY, wallZ),
                new Vector3(sideW, roomHeight, wallThick), wallColor);
            float aboveH = floorY + roomHeight - doorTopY;
            CreateRoomWall(parent, "WallFront_Top",
                new Vector3(doorCenterX, doorTopY + aboveH / 2f, wallZ),
                new Vector3(doorHalfW * 2f, aboveH, wallThick), wallColor);

            var doorPrefab = Resources.Load<GameObject>("RoomDecor/Door");
            if (doorPrefab == null) return;

            // Hinge on the -X edge; door swings into the room (+Z).
            float hingeX = doorCenterX - doorHalfW;
            var hingeGo = new GameObject("DoorHinge");
            hingeGo.transform.SetParent(parent, false);
            hingeGo.transform.position = new Vector3(hingeX, floorY, wallZ + wallThick * 0.5f);

            var doorGo = Instantiate(doorPrefab, hingeGo.transform);
            doorGo.name = "Door";
            var doorRends = doorGo.GetComponentsInChildren<Renderer>(true);
            if (doorRends.Length > 0)
            {
                var db = doorRends[0].bounds;
                foreach (var r in doorRends) db.Encapsulate(r.bounds);
                float glbH = db.size.y;
                float glbW = db.size.z > 0.0001f ? db.size.z : db.size.x;
                float uniformScale = glbH > 0.0001f ? doorH / glbH : 1f;
                float widthScale = glbW > 0.0001f ? (doorHalfW * 2f) / glbW : uniformScale;
                // Original door mesh faces +X with width on Z; rotate so it faces into the room (+Z).
                doorGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                doorGo.transform.localScale = new Vector3(uniformScale, uniformScale, widthScale);
            }
            else
            {
                doorGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // After Y=90, local +Z maps toward world -X; keep width along +X from the hinge.
            doorGo.transform.localPosition = new Vector3(doorHalfW, doorH * 0.5f, 0f);
            doorGo.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var col in doorGo.GetComponentsInChildren<Collider>())
                Destroy(col);
            foreach (var r in doorGo.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var intro = parent.gameObject.GetComponent<RoomDoorIntro>();
            if (intro == null) intro = parent.gameObject.AddComponent<RoomDoorIntro>();
            intro.Hinge = hingeGo.transform;
            intro.DoorwayCenter = new Vector3(doorCenterX, floorY + doorH * 0.45f, wallZ);
            intro.OutwardNormal = Vector3.back; // outside is -Z (same side as overview cam)
            intro.OpenAngle = 92f;
            intro.EyeHeight = floorY + doorH * 0.68f;
            intro.SnapClosed();
        }

        private void BuildWindowWall(Transform parent, float roomWidth, float roomHeight, float roomDepth,
            float wallThick, float floorY, Color wallColor, Color trimColor)
        {
            float wallX = roomWidth / 2f;
            float wallCenterY = floorY + roomHeight / 2f;

            // Window opening dimensions
            float winHalfD = roomDepth * 0.10f;                    // half-width along Z
            float winBotY = floorY + roomHeight * 0.28f;
            float winTopY = floorY + roomHeight * 0.62f;
            float winH = winTopY - winBotY;
            float winCenterY = (winBotY + winTopY) / 2f;

            // === 4 wall segments around the window opening ===
            // Far-Z segment
            float sideW = roomDepth / 2f - winHalfD;
            CreateRoomWall(parent, "WallRight_FarZ",
                new Vector3(wallX, wallCenterY, -(winHalfD + sideW / 2f)),
                new Vector3(wallThick, roomHeight, sideW), wallColor);
            // Near-Z segment
            CreateRoomWall(parent, "WallRight_NearZ",
                new Vector3(wallX, wallCenterY, winHalfD + sideW / 2f),
                new Vector3(wallThick, roomHeight, sideW), wallColor);
            // Above window
            float aboveH = floorY + roomHeight - winTopY;
            CreateRoomWall(parent, "WallRight_Top",
                new Vector3(wallX, winTopY + aboveH / 2f, 0),
                new Vector3(wallThick, aboveH, winHalfD * 2f), wallColor);
            // Below window
            float belowH = winBotY - floorY;
            CreateRoomWall(parent, "WallRight_Bottom",
                new Vector3(wallX, floorY + belowH / 2f, 0),
                new Vector3(wallThick, belowH, winHalfD * 2f), wallColor);

            // === Window frame ===
            float ft = roomHeight * 0.018f; // frame thickness proportional to room
            float fd = wallThick * 2f;      // frame depth (protrudes outward slightly)
            // Top bar
            CreateRoomWall(parent, "WinFrame_Top",
                new Vector3(wallX, winTopY + ft / 2f, 0),
                new Vector3(fd, ft, winHalfD * 2f + ft * 2f), trimColor);
            // Bottom bar
            CreateRoomWall(parent, "WinFrame_Bottom",
                new Vector3(wallX, winBotY - ft / 2f, 0),
                new Vector3(fd, ft, winHalfD * 2f + ft * 2f), trimColor);
            // Left bar
            CreateRoomWall(parent, "WinFrame_Left",
                new Vector3(wallX, winCenterY, -(winHalfD + ft / 2f)),
                new Vector3(fd, winH + ft * 2f, ft), trimColor);
            // Right bar
            CreateRoomWall(parent, "WinFrame_Right",
                new Vector3(wallX, winCenterY, winHalfD + ft / 2f),
                new Vector3(fd, winH + ft * 2f, ft), trimColor);
            // Horizontal cross-bar
            CreateRoomWall(parent, "WinFrame_CrossH",
                new Vector3(wallX, winCenterY, 0),
                new Vector3(fd, ft, winHalfD * 2f), trimColor);
            // Vertical cross-bar
            CreateRoomWall(parent, "WinFrame_CrossV",
                new Vector3(wallX, winCenterY, 0),
                new Vector3(fd, winH, ft * 0.6f), trimColor);

            // === Window sill (interior ledge) ===
            float sillDepth = roomWidth * 0.02f;
            float sillH = ft * 1.5f;
            var sill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sill.name = "WindowSill";
            sill.transform.SetParent(parent, false);
            sill.transform.position = new Vector3(wallX - sillDepth / 2f, winBotY - sillH / 2f, 0);
            sill.transform.localScale = new Vector3(sillDepth, sillH, winHalfD * 2f + ft * 2f);
            var sillMat = sill.GetComponent<Renderer>().material;
            sillMat.color = new Color(0.95f, 0.92f, 0.88f);
            sillMat.SetFloat("_Glossiness", 0.4f);
            sill.layer = LayerMask.NameToLayer("Ignore Raycast");
            sill.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // === Glass pane ===
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "WindowGlass";
            glass.transform.SetParent(parent, false);
            glass.transform.position = new Vector3(wallX, winCenterY, 0);
            glass.transform.localScale = new Vector3(wallThick * 0.25f, winH, winHalfD * 2f);
            var standardShader = Shader.Find("Standard");
            var glassMat = standardShader != null ? new Material(standardShader) : glass.GetComponent<Renderer>().material;
            glassMat.color = new Color(0.75f, 0.90f, 1f, 0.18f);
            glassMat.SetFloat("_Metallic", 0f);
            glassMat.SetFloat("_Glossiness", 0.97f);
            glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glassMat.SetInt("_ZWrite", 0);
            glassMat.DisableKeyword("_ALPHATEST_ON");
            glassMat.EnableKeyword("_ALPHABLEND_ON");
            glassMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glassMat.renderQueue = 3000;
            var glassRenderer = glass.GetComponent<Renderer>();
            glassRenderer.material = glassMat;
            glassRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glassRenderer.receiveShadows = false;
            glass.layer = LayerMask.NameToLayer("Ignore Raycast");
            Destroy(glass.GetComponent<Collider>());

            // === Sky backdrop outside the window ===
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "WindowSkyBackdrop";
            sky.transform.SetParent(parent, false);
            sky.transform.position = new Vector3(wallX + wallThick, winCenterY, 0);
            sky.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            sky.transform.localScale = new Vector3(winHalfD * 2.5f, winH * 1.3f, 1f);
            var unlitShader = Shader.Find("Unlit/Color");
            var skyMat = unlitShader != null ? new Material(unlitShader) : sky.GetComponent<Renderer>().material;
            skyMat.color = new Color(0.53f, 0.80f, 1f);
            sky.GetComponent<Renderer>().material = skyMat;
            sky.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sky.layer = LayerMask.NameToLayer("Ignore Raycast");
            Destroy(sky.GetComponent<Collider>());

        }

        private void CreateRoomWall(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            var mat = wall.GetComponent<Renderer>().material;
            mat.color = color;
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0.15f);
            wall.layer = LayerMask.NameToLayer("Ignore Raycast");
            // Walls receive shadows but don't cast them (otherwise they block the directional light)
            wall.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Places the RoomDecor/shelf GLB on the left wall (away from door + window).
        /// </summary>
        private void PlaceRoomShelfAsset(Transform parent, float roomWidth, float roomHeight, float roomDepth,
            float wallThick, float floorY)
        {
            var shelfPrefab = Resources.Load<GameObject>("RoomDecor/shelf");
            if (shelfPrefab == null) return;

            float targetH = roomHeight * 0.42f;
            float targetW = roomWidth * 0.22f;
            // Sit near the floor (just above baseboard), not mid-wall
            float bottomY = floorY + 0.12f;
            float leftX = -roomWidth / 2f + wallThick;
            // Slightly toward the back so it reads clearly from the door/overview side
            float z = roomDepth * 0.18f;

            var go = Instantiate(shelfPrefab, parent);
            go.name = "Shelf";
            go.transform.SetPositionAndRotation(
                new Vector3(leftX, bottomY, z),
                Quaternion.Euler(0f, -90f, 0f)); // front faces into the room (+X)

            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float h = Mathf.Max(0.001f, b.size.y);
                float w = Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
                go.transform.localScale = Vector3.one * Mathf.Min(targetH / h, targetW / w);

                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Vector3 inward = Vector3.right;
                float lift = bottomY - b.min.y;
                float push = Vector3.Dot(new Vector3(leftX, bottomY, z) - b.center, inward)
                    + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), Mathf.Abs(inward.y), Mathf.Abs(inward.z)))
                    + 0.05f;
                go.transform.position = new Vector3(leftX, bottomY, z) + Vector3.up * lift + inward * push;
            }

            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ignore;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Destroy(col);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        /// <summary>
        /// Places RoomDecor/WallMap on the back wall (+Z). The GLB is a flat XZ map,
        /// so it is tilted upright to face into the room.
        /// </summary>
        private void PlaceRoomWallMap(Transform parent, float roomWidth, float roomHeight, float roomDepth,
            float wallThick, float floorY)
        {
            var prefab = Resources.Load<GameObject>("RoomDecor/WallMap");
            if (prefab == null) return;

            float targetH = roomHeight * 0.32f;
            float targetW = roomWidth * 0.28f;
            float centerY = floorY + roomHeight * 0.48f;
            float backZ = roomDepth / 2f - wallThick;
            Vector3 anchor = new Vector3(0f, centerY, backZ);

            var go = Instantiate(prefab, parent);
            go.name = "WallMap";
            // Flat on XZ originally; -90° X makes +Y face into the room (-Z).
            go.transform.SetPositionAndRotation(anchor, Quaternion.Euler(-90f, 0f, 0f));

            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float h = Mathf.Max(0.001f, b.size.y);
                float w = Mathf.Max(0.001f, b.size.x);
                go.transform.localScale = Vector3.one * Mathf.Min(targetH / h, targetW / w);

                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Vector3 inward = Vector3.back;
                float push = Vector3.Dot(anchor - b.center, inward)
                    + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), Mathf.Abs(inward.y), Mathf.Abs(inward.z)))
                    + 0.08f;
                float lift = centerY - b.center.y;
                go.transform.position = anchor + Vector3.up * lift + inward * push;
            }

            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ignore;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Destroy(col);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        private static void FillSkyFace(Color[] pixels, int size, Color top, Color horizon, Color bottom)
        {
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                Color c;
                if (t < 0.5f)
                    c = Color.Lerp(bottom, horizon, t * 2f);
                else
                    c = Color.Lerp(horizon, top, (t - 0.5f) * 2f);
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = c;
            }
        }

        private void CreateUI()
        {
            Debug.Log("[Sandplay] CreateUI starting...");

            // Create Canvas
            _canvasGo = new GameObject("Canvas");
            var canvasGo = _canvasGo;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Safe area container — keeps UI within screen safe area on notched devices
            var safeArea = new GameObject("SafeArea");
            safeArea.transform.SetParent(canvasGo.transform, false);
            var safeAreaRT = safeArea.AddComponent<RectTransform>();
            safeAreaRT.anchorMin = Vector2.zero;
            safeAreaRT.anchorMax = Vector2.one;
            safeAreaRT.offsetMin = Vector2.zero;
            safeAreaRT.offsetMax = Vector2.zero;
            safeArea.AddComponent<Sandplay.UI.SafeAreaFitter>();
            _safeArea = safeArea;

            // Sandbox UI container (hidden when in menu)
            _sandboxUI = new GameObject("SandboxUI");
            _sandboxUI.transform.SetParent(safeArea.transform, false);
            var sandboxUIRT = _sandboxUI.AddComponent<RectTransform>();
            sandboxUIRT.anchorMin = Vector2.zero;
            sandboxUIRT.anchorMax = Vector2.one;
            sandboxUIRT.offsetMin = Vector2.zero;
            sandboxUIRT.offsetMax = Vector2.zero;

            // === Left-side vertical toolbar ===
            float toolbarWidth = 70f;
            float btnSize = 50f;
            float btnPad = 4f;
            var sandRow = CreatePanel(_sandboxUI.transform, "SandToolBar",
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(toolbarWidth, 0));
            sandRow.GetComponent<Image>().color = new Color(0.065f, 0.085f, 0.10f, 0.96f);

            float sy = -btnPad; // current Y offset from top

            var toolLabel = CreateText(sandRow.transform, "ToolLabel", Localization.Get("toolbar.sand"), 12,
                new Vector2(0, sy - 20), new Vector2(toolbarWidth, sy));
            toolLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            TrackLocalized(toolLabel.GetComponent<TextMeshProUGUI>(), "toolbar.sand");
            sy -= 22;

            // === Sand Surface Material Picker (top of sidebar) ===
            // Note: iOS restricts splatmap operations, so this feature is disabled on iOS
#if !UNITY_IOS || UNITY_EDITOR
            var matBtn = CreateVerticalToolButton(sandRow.transform, "Btn_SandMaterial", Localization.Get("tool.sand_material"),
                toolbarWidth, btnSize, ref sy, btnPad);
            TrackLocalized(matBtn.GetComponentInChildren<TextMeshProUGUI>(), "tool.sand_material");
            float matButtonTop = sy + btnSize + btnPad;

            // Build flyout panel (hidden by default, anchored right of toolbar)
            var matPanel = new GameObject("SandMaterialPanel");
            matPanel.transform.SetParent(_sandboxUI.transform, false);
            var matPanelImg = matPanel.AddComponent<Image>();
            matPanelImg.color = new Color(0.14f, 0.14f, 0.18f, 0.96f);
            ApplyRoundedCorners(matPanelImg);
            var matPanelRT = matPanel.GetComponent<RectTransform>();
            matPanelRT.anchorMin = new Vector2(0, 1);
            matPanelRT.anchorMax = new Vector2(0, 1);
            matPanelRT.pivot = new Vector2(0, 1);
            matPanelRT.anchoredPosition = new Vector2(toolbarWidth + 8f, matButtonTop);
            int materialRows = Mathf.CeilToInt(Sandplay.Sand.SandMaterialController.PresetNames.Length / 2f);
            matPanelRT.sizeDelta = new Vector2(228f, 26f + materialRows * 44f + 6f);
            matPanel.SetActive(false);

            // Header label inside flyout
            var matHeaderGo = new GameObject("MatHeader");
            matHeaderGo.transform.SetParent(matPanel.transform, false);
            var matHeaderTxt = matHeaderGo.AddComponent<TextMeshProUGUI>();
            matHeaderTxt.text = Localization.Get("tool.sand_material").ToUpper();
            matHeaderTxt.fontSize = 11;
            matHeaderTxt.alignment = TextAlignmentOptions.Center;
            matHeaderTxt.color = new Color(0.65f, 0.65f, 0.70f);
            matHeaderTxt.font = GetUIFont();
            var matHeaderRT = matHeaderGo.GetComponent<RectTransform>();
            matHeaderRT.anchorMin = new Vector2(0, 1);
            matHeaderRT.anchorMax = new Vector2(1, 1);
            matHeaderRT.pivot = new Vector2(0.5f, 1);
            matHeaderRT.anchoredPosition = new Vector2(0, -2f);
            matHeaderRT.sizeDelta = new Vector2(0, 14f);

            // Compact two-column material grid inside the attached flyout.
            // Selecting a preset sets it as the active paint material and activates SandPaint tool
            string[] matLabelKeys = { "mat.sand", "mat.rock", "mat.grass", "mat.snow", "mat.mud", "mat.water", "mat.clay" };
            var matBtns = new Button[Sandplay.Sand.SandMaterialController.PresetNames.Length];
            for (int mi = 0; mi < Sandplay.Sand.SandMaterialController.PresetNames.Length; mi++)
            {
                var presetIndex = mi;
                var presetColor = Sandplay.Sand.SandMaterialController.PresetColors[mi];
                var mbGo = new GameObject($"Btn_Mat_{presetIndex}");
                mbGo.transform.SetParent(matPanel.transform, false);
                var mbImg = mbGo.AddComponent<Image>();
                mbImg.color = presetColor;
                ApplyRoundedCorners(mbImg);
                var mbRT = mbGo.GetComponent<RectTransform>();
                int column = presetIndex % 2;
                int row = presetIndex / 2;
                mbRT.anchorMin = mbRT.anchorMax = new Vector2(0, 1);
                mbRT.pivot = new Vector2(0, 1);
                mbRT.anchoredPosition = new Vector2(6f + column * 110f, -24f - row * 44f);
                mbRT.sizeDelta = new Vector2(106f, 40f);
                var mbBtn = mbGo.AddComponent<Button>();
                var mbColors = mbBtn.colors;
                mbColors.highlightedColor = presetColor * 1.25f;
                mbColors.pressedColor = presetColor * 0.75f;
                mbBtn.colors = mbColors;
                var mbTxtGo = new GameObject("Label");
                mbTxtGo.transform.SetParent(mbGo.transform, false);
                var mbTxt = mbTxtGo.AddComponent<TextMeshProUGUI>();
                mbTxt.text = Localization.Get(matLabelKeys[mi]);
                mbTxt.fontSize = 13;
                mbTxt.fontStyle = FontStyles.Bold;
                mbTxt.alignment = TextAlignmentOptions.Center;
                mbTxt.color = (mi == 0 || mi == 3) ? new Color(0.15f, 0.15f, 0.20f) : Color.white;
                mbTxt.font = GetUIFont();
                var mbTxtRT = mbTxtGo.GetComponent<RectTransform>();
                mbTxtRT.anchorMin = Vector2.zero;
                mbTxtRT.anchorMax = Vector2.one;
                mbTxtRT.offsetMin = Vector2.zero;
                mbTxtRT.offsetMax = Vector2.zero;
                TrackLocalized(mbTxt, matLabelKeys[mi]);
                matBtns[mi] = mbBtn;

                // Update the "Surface" toolbar button color to show active material
                var matBtnImg = matBtn.GetComponent<Image>();

                mbBtn.onClick.AddListener(() =>
                {
                    var ctrl = Sandplay.Sand.SandMaterialController.Instance;
                    if (ctrl != null) ctrl.SelectedPreset = presetIndex;

                    // Tint the toolbar button to show selected material
                    if (matBtnImg != null)
                        matBtnImg.color = presetColor;

                    // Outline selected preset button
                    for (int mj = 0; mj < matBtns.Length; mj++)
                    {
                        var outline = matBtns[mj].GetComponent<UnityEngine.UI.Outline>();
                        if (outline == null) outline = matBtns[mj].gameObject.AddComponent<UnityEngine.UI.Outline>();
                        outline.effectColor = mj == presetIndex ? Color.white : Color.clear;
                        outline.effectDistance = new Vector2(2, -2);
                    }

                    // Close flyout and activate paint tool
                    matPanel.SetActive(false);
                    GameManager.Instance.SetToolMode(ToolMode.SandPaint);
                });
            }

            // Toolbar button toggles flyout; if paint mode already active, just toggle flyout
            matBtn.onClick.AddListener(() => matPanel.SetActive(!matPanel.activeSelf));

            // Dim the surface button when paint mode is not active
            EventBus.Subscribe<ToolModeChangedEvent>(evt =>
            {
                var matBtnImg2 = matBtn.GetComponent<Image>();
                if (matBtnImg2 == null) return;
                if (evt.NewMode != ToolMode.SandPaint)
                {
                    var ctrl = Sandplay.Sand.SandMaterialController.Instance;
                    var activeColor = ctrl != null
                        ? Sandplay.Sand.SandMaterialController.PresetColors[ctrl.SelectedPreset]
                        : new Color(0.22f, 0.22f, 0.27f, 1f);
                    matBtnImg2.color = new Color(activeColor.r * 0.6f, activeColor.g * 0.6f, activeColor.b * 0.6f, 1f);
                    matPanel.SetActive(false);
                }
            });
#endif

            // Separator
            sy -= 8;

            // === Raise / Dig / Flatten ===
            string[] sandKeys = { "tool.raise", "tool.dig", "tool.flatten" };
            string[] sandNames = { Localization.Get(sandKeys[0]), Localization.Get(sandKeys[1]), Localization.Get(sandKeys[2]) };
            ToolMode[] sandModes = {
                ToolMode.SandRaise, ToolMode.SandDig,
                ToolMode.SandFlatten
            };
            var sandButtons = new Button[sandNames.Length];
            var sandButtonDefaultColor = new Color(0.12f, 0.15f, 0.17f, 1f);
            var sandButtonActiveColor = new Color(0.15f, 0.38f, 0.40f, 1f);
            for (int i = 0; i < sandNames.Length; i++)
            {
                var btn = CreateVerticalToolButton(sandRow.transform, $"Btn_{sandNames[i]}", sandNames[i],
                    toolbarWidth, btnSize, ref sy, btnPad);
                TrackLocalized(btn.GetComponentInChildren<TextMeshProUGUI>(), sandKeys[i]);
                var icon = btn.transform.Find("Icon_plus");
                if (icon != null) icon.gameObject.SetActive(false);
                foreach (var image in btn.GetComponentsInChildren<Image>())
                    if (image.gameObject != btn.gameObject) image.enabled = false;
                var terrainIcon = new GameObject("TerrainGlyph", typeof(RectTransform));
                terrainIcon.transform.SetParent(btn.transform, false);
                var terrainGlyph = terrainIcon.AddComponent<TerrainToolGlyph>();
                terrainGlyph.Mode = i;
                terrainGlyph.color = new Color(.84f,.89f,.91f);
                terrainGlyph.raycastTarget = false;
                var terrainRT = terrainIcon.GetComponent<RectTransform>();
                terrainRT.anchorMin = terrainRT.anchorMax = new Vector2(.5f,.65f);
                terrainRT.sizeDelta = new Vector2(22,22);
                var mode = sandModes[i];
                btn.onClick.AddListener(() =>
                {
                    // Toggle off if clicking the same tool again
                    if (GameManager.Instance.CurrentTool == mode)
                        GameManager.Instance.SetToolMode(ToolMode.ObjectSelect);
                    else
                        GameManager.Instance.SetToolMode(mode);
                });
                sandButtons[i] = btn;
            }

            // Highlight active tool button
            EventBus.Subscribe<ToolModeChangedEvent>(evt =>
            {
                for (int j = 0; j < sandModes.Length; j++)
                {
                    var img = sandButtons[j].GetComponent<Image>();
                    img.color = evt.NewMode == sandModes[j] ? sandButtonActiveColor : sandButtonDefaultColor;
                }
            });

            // Separator
            sy -= 8;

            var walkBtn = CreateVerticalToolButton(sandRow.transform, "Btn_Walk", Localization.Get("tool.walk"),
                toolbarWidth, btnSize, ref sy, btnPad);
            TrackLocalized(walkBtn.GetComponentInChildren<TextMeshProUGUI>(), "tool.walk");
            walkBtn.onClick.AddListener(() => GameManager.Instance.SetToolMode(ToolMode.WalkMode));

            // Separator
            sy -= 8;

            var settingsBtn = CreateVerticalToolButton(sandRow.transform, "Btn_Settings", Localization.Get("tool.settings"),
                toolbarWidth, btnSize, ref sy, btnPad);
            TrackLocalized(settingsBtn.GetComponentInChildren<TextMeshProUGUI>(), "tool.settings");
            settingsBtn.onClick.AddListener(() => ToggleSettingsPanel());

            // Separator
            sy -= 8;

            var analyzeBtn = CreateVerticalToolButton(sandRow.transform, "Btn_Analyze", Localization.Get("tool.ai_analysis"),
                toolbarWidth, btnSize, ref sy, btnPad);
            TrackLocalized(analyzeBtn.GetComponentInChildren<TextMeshProUGUI>(), "tool.ai_analysis");
            analyzeBtn.onClick.AddListener(() =>
            {
                if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn)
                    OpenLoginScreen();
                else
                    EventBus.Publish(new AnalysisRequestedEvent());
            });

            // Separator
            sy -= 8;

            var manualBtn = CreateVerticalToolButton(sandRow.transform, "Btn_ManualReport", Localization.Get("report.reports"),
                toolbarWidth, btnSize, ref sy, btnPad);
            TrackLocalized(manualBtn.GetComponentInChildren<TextMeshProUGUI>(), "report.reports");
            manualBtn.onClick.AddListener(() => ToggleManualReportPanel());

            // Center the complete group in the available sidebar height, retaining
            // existing spacing between controls and adapting to window resizing.
            float toolbarGroupHeight = -sy;
            foreach (RectTransform child in sandRow.transform)
            {
                child.anchorMin = new Vector2(child.anchorMin.x, .5f);
                child.anchorMax = new Vector2(child.anchorMax.x, .5f);
                child.anchoredPosition += new Vector2(0, toolbarGroupHeight * .5f);
            }

            ApplyHomeRoundedCorners(sandRow.GetComponent<Image>(), 12f);
            sandRow.AddComponent<ContentSizedToolDock>().Initialize(toolbarWidth, toolbarGroupHeight);

            // Forward-declare so the lambdas below can capture them
            GameObject brushPanel = null;
            GameObject exitBtnGo = null;
            Button catalogBtn = null;

            // === Exit button (top-left, next to toolbar, returns to main menu) ===
            exitBtnGo = new GameObject("Btn_Exit");
            exitBtnGo.transform.SetParent(_sandboxUI.transform, false);
            var exitBtnRT = exitBtnGo.AddComponent<RectTransform>();
            exitBtnRT.anchorMin = new Vector2(0, 1);
            exitBtnRT.anchorMax = new Vector2(0, 1);
            exitBtnRT.pivot = new Vector2(0, 1);
            exitBtnRT.anchoredPosition = new Vector2(toolbarWidth + 4, -4);
            exitBtnRT.sizeDelta = new Vector2(50, 36);
            var exitBtnImg = exitBtnGo.AddComponent<Image>();
            exitBtnImg.color = new Color(0.48f, 0.26f, 0.26f, 0.92f);
            ApplyRoundedCorners(exitBtnImg);
            var exitBtn = exitBtnGo.AddComponent<Button>();
            exitBtn.onClick.AddListener(() => ShowSandboxExitConfirmation(ReturnToMenu));
            var exitBtnTxtGo = new GameObject("Label");
            exitBtnTxtGo.transform.SetParent(exitBtnGo.transform, false);
            var exitBtnTxt = exitBtnTxtGo.AddComponent<TextMeshProUGUI>();
            exitBtnTxt.text = Localization.Get("tool.exit");
            TrackLocalized(exitBtnTxt, "tool.exit");
            exitBtnTxt.fontSize = 14;
            exitBtnTxt.alignment = TextAlignmentOptions.Center;
            exitBtnTxt.color = Color.white;
            exitBtnTxt.font = GetUIFont();
            var exitBtnTxtRT = exitBtnTxtGo.GetComponent<RectTransform>();
            exitBtnTxtRT.anchorMin = Vector2.zero;
            exitBtnTxtRT.anchorMax = Vector2.one;
            exitBtnTxtRT.offsetMin = Vector2.zero;
            exitBtnTxtRT.offsetMax = Vector2.zero;

            // === Undo / Redo bar (top-left, after exit button) ===
            var undoRedoBar = new GameObject("UndoRedoBar");
            undoRedoBar.transform.SetParent(_sandboxUI.transform, false);
            var undoRedoRT = undoRedoBar.AddComponent<RectTransform>();
            undoRedoRT.anchorMin = new Vector2(0, 1);
            undoRedoRT.anchorMax = new Vector2(0, 1);
            undoRedoRT.pivot = new Vector2(0, 1);
            undoRedoRT.anchoredPosition = new Vector2(toolbarWidth + 58, -4);
            undoRedoRT.sizeDelta = new Vector2(134, 36);
            var undoRedoBg = undoRedoBar.AddComponent<Image>();
            undoRedoBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            ApplyRoundedCorners(undoRedoBg);

            var undoLayout = undoRedoBar.AddComponent<HorizontalLayoutGroup>();
            undoLayout.spacing = 2;
            undoLayout.padding = new RectOffset(2, 2, 2, 2);
            undoLayout.childAlignment = TextAnchor.MiddleCenter;
            undoLayout.childForceExpandWidth = true;
            undoLayout.childForceExpandHeight = true;

            // Undo button
            var undoBtnGo = new GameObject("Btn_Undo");
            undoBtnGo.transform.SetParent(undoRedoBar.transform, false);
            var undoBtnImg = undoBtnGo.AddComponent<Image>();
            undoBtnImg.color = new Color(0.22f, 0.22f, 0.27f, 1f);
            ApplyRoundedCorners(undoBtnImg);
            var undoBtn = undoBtnGo.AddComponent<Button>();
            var undoBtnColors = undoBtn.colors;
            undoBtnColors.highlightedColor = new Color(0.34f, 0.34f, 0.40f);
            undoBtnColors.pressedColor = new Color(0.18f, 0.36f, 0.46f);
            undoBtn.colors = undoBtnColors;
            Sprite undoIcon = LoadIconWhiteTinted("Undo");
            if (undoIcon != null)
            {
                var undoIconGo = new GameObject("Icon");
                undoIconGo.transform.SetParent(undoBtnGo.transform, false);
                var undoIconImg = undoIconGo.AddComponent<Image>();
                undoIconImg.sprite = undoIcon;
                undoIconImg.preserveAspect = true;
                undoIconImg.raycastTarget = false;
                var undoIconRT = undoIconGo.GetComponent<RectTransform>();
                undoIconRT.anchorMin = new Vector2(0.15f, 0.15f);
                undoIconRT.anchorMax = new Vector2(0.85f, 0.85f);
                undoIconRT.offsetMin = Vector2.zero;
                undoIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var undoLblGo = new GameObject("Label");
                undoLblGo.transform.SetParent(undoBtnGo.transform, false);
                var undoLbl = undoLblGo.AddComponent<TextMeshProUGUI>();
                undoLbl.text = "\u21A9";
                undoLbl.fontSize = 20;
                undoLbl.alignment = TextAlignmentOptions.Center;
                undoLbl.color = Color.white;
                undoLbl.font = GetUIFont();
                var undoLblRT = undoLblGo.GetComponent<RectTransform>();
                undoLblRT.anchorMin = Vector2.zero;
                undoLblRT.anchorMax = Vector2.one;
                undoLblRT.offsetMin = Vector2.zero;
                undoLblRT.offsetMax = Vector2.zero;
            }
            undoBtn.onClick.AddListener(() => Sandplay.Data.UndoManager.Instance?.UndoLast());

            // Redo button
            var redoBtnGo = new GameObject("Btn_Redo");
            redoBtnGo.transform.SetParent(undoRedoBar.transform, false);
            var redoBtnImg = redoBtnGo.AddComponent<Image>();
            redoBtnImg.color = new Color(0.22f, 0.22f, 0.27f, 1f);
            ApplyRoundedCorners(redoBtnImg);
            var redoBtn = redoBtnGo.AddComponent<Button>();
            var redoBtnColors = redoBtn.colors;
            redoBtnColors.highlightedColor = new Color(0.34f, 0.34f, 0.40f);
            redoBtnColors.pressedColor = new Color(0.18f, 0.36f, 0.46f);
            redoBtn.colors = redoBtnColors;
            Sprite redoIcon = LoadIconWhiteTinted("Redo");
            if (redoIcon != null)
            {
                var redoIconGo = new GameObject("Icon");
                redoIconGo.transform.SetParent(redoBtnGo.transform, false);
                var redoIconImg = redoIconGo.AddComponent<Image>();
                redoIconImg.sprite = redoIcon;
                redoIconImg.preserveAspect = true;
                redoIconImg.raycastTarget = false;
                var redoIconRT = redoIconGo.GetComponent<RectTransform>();
                redoIconRT.anchorMin = new Vector2(0.15f, 0.15f);
                redoIconRT.anchorMax = new Vector2(0.85f, 0.85f);
                redoIconRT.offsetMin = Vector2.zero;
                redoIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var redoLblGo = new GameObject("Label");
                redoLblGo.transform.SetParent(redoBtnGo.transform, false);
                var redoLbl = redoLblGo.AddComponent<TextMeshProUGUI>();
                redoLbl.text = "\u21AA";
                redoLbl.fontSize = 20;
                redoLbl.alignment = TextAlignmentOptions.Center;
                redoLbl.color = Color.white;
                redoLbl.font = GetUIFont();
                var redoLblRT = redoLblGo.GetComponent<RectTransform>();
                redoLblRT.anchorMin = Vector2.zero;
                redoLblRT.anchorMax = Vector2.one;
                redoLblRT.offsetMin = Vector2.zero;
                redoLblRT.offsetMax = Vector2.zero;
            }
            redoBtn.onClick.AddListener(() => Sandplay.Data.UndoManager.Instance?.RedoLast());

            // Reset camera to the centered intro view
            var resetViewBtnGo = new GameObject("Btn_ResetView");
            resetViewBtnGo.transform.SetParent(undoRedoBar.transform, false);
            var resetViewBtnImg = resetViewBtnGo.AddComponent<Image>();
            resetViewBtnImg.color = new Color(0.22f, 0.22f, 0.27f, 1f);
            ApplyRoundedCorners(resetViewBtnImg);
            var resetViewBtn = resetViewBtnGo.AddComponent<Button>();
            var resetViewBtnColors = resetViewBtn.colors;
            resetViewBtnColors.highlightedColor = new Color(0.34f, 0.34f, 0.40f);
            resetViewBtnColors.pressedColor = new Color(0.18f, 0.36f, 0.46f);
            resetViewBtn.colors = resetViewBtnColors;

            Sprite resetViewIcon = LoadIconWhiteTinted("Camera Reset");
            if (resetViewIcon != null)
            {
                var resetViewIconGo = new GameObject("Icon");
                resetViewIconGo.transform.SetParent(resetViewBtnGo.transform, false);
                var resetViewIconImg = resetViewIconGo.AddComponent<Image>();
                resetViewIconImg.sprite = resetViewIcon;
                resetViewIconImg.preserveAspect = true;
                resetViewIconImg.raycastTarget = false;
                var resetViewIconRT = resetViewIconGo.GetComponent<RectTransform>();
                resetViewIconRT.anchorMin = new Vector2(0.15f, 0.15f);
                resetViewIconRT.anchorMax = new Vector2(0.85f, 0.85f);
                resetViewIconRT.offsetMin = Vector2.zero;
                resetViewIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var resetViewLblGo = new GameObject("Label");
                resetViewLblGo.transform.SetParent(resetViewBtnGo.transform, false);
                var resetViewLbl = resetViewLblGo.AddComponent<TextMeshProUGUI>();
                resetViewLbl.text = "\u2302";
                resetViewLbl.fontSize = 20;
                resetViewLbl.alignment = TextAlignmentOptions.Center;
                resetViewLbl.color = Color.white;
                resetViewLbl.font = GetUIFont();
                resetViewLbl.raycastTarget = false;
                var resetViewLblRT = resetViewLblGo.GetComponent<RectTransform>();
                resetViewLblRT.anchorMin = Vector2.zero;
                resetViewLblRT.anchorMax = Vector2.one;
                resetViewLblRT.offsetMin = Vector2.zero;
                resetViewLblRT.offsetMax = Vector2.zero;
            }
            resetViewBtn.onClick.AddListener(() =>
                FindAnyObjectByType<Sandplay.Camera.SandboxCamera>()?.ResetToIntroView());

            // Exit, Undo/Redo, and Reset View are high-priority — ensure they always render and receive input
            // on top of any overlapping panels (e.g. BrushSettings, CatalogPanel) by giving
            // each its own Canvas override with a higher sorting order.
            var exitBtnCanvas = exitBtnGo.AddComponent<Canvas>();
            exitBtnCanvas.overrideSorting = true;
            exitBtnCanvas.sortingOrder = 50;
            exitBtnGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var undoRedoCanvas = undoRedoBar.AddComponent<Canvas>();
            undoRedoCanvas.overrideSorting = true;
            undoRedoCanvas.sortingOrder = 50;
            undoRedoBar.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // === Network info overlay (bottom-right, horizontal: QR | code | copy) ===
            var netOverlay = new GameObject("NetOverlay");
            netOverlay.transform.SetParent(_sandboxUI.transform, false);
            var netOverlayRT = netOverlay.AddComponent<RectTransform>();
            netOverlayRT.anchorMin = new Vector2(1f, 0f);
            netOverlayRT.anchorMax = new Vector2(1f, 0f);
            netOverlayRT.pivot = new Vector2(1f, 0f);
            netOverlayRT.anchoredPosition = new Vector2(-6, 30);
            netOverlayRT.sizeDelta = new Vector2(260, 48);
            var netOverlayBg = netOverlay.AddComponent<Image>();
            netOverlayBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            ApplyRoundedCorners(netOverlayBg);
            netOverlay.SetActive(false);
            _netOverlayGo = netOverlay;

            // QR code image (left, square, vertically centered)
            var qrGo = new GameObject("QRCode");
            qrGo.transform.SetParent(netOverlay.transform, false);
            _qrCodeImage = qrGo.AddComponent<Image>();
            _qrCodeImage.color = Color.white;
            _qrCodeImage.preserveAspect = true;
            var qrBtn = qrGo.AddComponent<Button>();
            qrBtn.targetGraphic = _qrCodeImage;
            var qrBtnColors = qrBtn.colors;
            qrBtnColors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            qrBtnColors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            qrBtn.colors = qrBtnColors;
            qrBtn.onClick.AddListener(() => ShowQRCodeDialog());
            var qrRT = qrGo.GetComponent<RectTransform>();
            qrRT.anchorMin = new Vector2(0, 0);
            qrRT.anchorMax = new Vector2(0, 1);
            qrRT.pivot = new Vector2(0, 0.5f);
            qrRT.offsetMin = new Vector2(4, 4);
            qrRT.offsetMax = new Vector2(44, -4);

            // Room code (middle, vertically centered)
            var roomCodeGo = new GameObject("RoomCode");
            roomCodeGo.transform.SetParent(netOverlay.transform, false);
            _roomCodeText = roomCodeGo.AddComponent<TextMeshProUGUI>();
            _roomCodeText.font = GetUIFont();
            _roomCodeText.fontSize = 16;
            _roomCodeText.color = new Color(0.56f, 0.76f, 0.82f, 1f);
            _roomCodeText.fontStyle = FontStyles.Bold;
            _roomCodeText.alignment = TextAlignmentOptions.Left;
            var roomRT = roomCodeGo.GetComponent<RectTransform>();
            roomRT.anchorMin = new Vector2(0, 0);
            roomRT.anchorMax = new Vector2(1, 1);
            roomRT.offsetMin = new Vector2(48, 0);
            roomRT.offsetMax = new Vector2(-34, 0);

            // Copy button (small, right side, vertically centered)
            var copyBtnGo = new GameObject("CopyRoomCodeBtn");
            copyBtnGo.transform.SetParent(netOverlay.transform, false);
            var copyBtnRT = copyBtnGo.AddComponent<RectTransform>();
            copyBtnRT.anchorMin = new Vector2(1, 0.5f);
            copyBtnRT.anchorMax = new Vector2(1, 0.5f);
            copyBtnRT.pivot = new Vector2(1, 0.5f);
            copyBtnRT.anchoredPosition = new Vector2(-4, 0);
            copyBtnRT.sizeDelta = new Vector2(28, 28);
            var copyBtnImg = copyBtnGo.AddComponent<Image>();
            copyBtnImg.color = new Color(0.22f, 0.22f, 0.27f, 1f);
            ApplyRoundedCorners(copyBtnImg);
            var copyBtn = copyBtnGo.AddComponent<Button>();
            copyBtn.targetGraphic = copyBtnImg;
            copyBtn.onClick.AddListener(() =>
            {
                var net = NetworkBootstrapper.Instance;
                if (net != null && !string.IsNullOrEmpty(net.RoomCode))
                {
                    GUIUtility.systemCopyBuffer = net.RoomCode;
                    Debug.Log($"Copied room code to clipboard: {net.RoomCode}");
                }
            });

            // Copy icon
            Sprite copyIconSprite = LoadIconWhiteTinted("copy");
            if (copyIconSprite != null)
            {
                var copyIconGo = new GameObject("CopyIcon");
                copyIconGo.transform.SetParent(copyBtnGo.transform, false);
                var copyIconImg = copyIconGo.AddComponent<Image>();
                copyIconImg.sprite = copyIconSprite;
                copyIconImg.preserveAspect = true;
                var copyIconRT = copyIconGo.GetComponent<RectTransform>();
                copyIconRT.anchorMin = new Vector2(0.15f, 0.15f);
                copyIconRT.anchorMax = new Vector2(0.85f, 0.85f);
                copyIconRT.offsetMin = Vector2.zero;
                copyIconRT.offsetMax = Vector2.zero;
            }

            // Network status (viewer count — below the overlay)
            var netStatusGo = new GameObject("NetStatus");
            netStatusGo.transform.SetParent(netOverlay.transform, false);
            _networkStatusText = netStatusGo.AddComponent<TextMeshProUGUI>();
            _networkStatusText.font = GetUIFont();
            _networkStatusText.fontSize = 10;
            _networkStatusText.color = new Color(0.60f, 0.78f, 0.68f, 1f);
            _networkStatusText.alignment = TextAlignmentOptions.Center;
            var netStatusRT = netStatusGo.GetComponent<RectTransform>();
            netStatusRT.anchorMin = new Vector2(0, 0);
            netStatusRT.anchorMax = new Vector2(1, 0);
            netStatusRT.pivot = new Vector2(0.5f, 1f);
            netStatusRT.anchoredPosition = new Vector2(0, 0);
            netStatusRT.sizeDelta = new Vector2(260, 16);

            // Spectator badge (top center, below net overlay)
            _spectatorBadge = new GameObject("SpectatorBadge");
            _spectatorBadge.transform.SetParent(_sandboxUI.transform, false);
            var badgeRT = _spectatorBadge.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.5f, 1f);
            badgeRT.anchorMax = new Vector2(0.5f, 1f);
            badgeRT.pivot = new Vector2(0.5f, 1f);
            badgeRT.anchoredPosition = new Vector2(0, -58);
            badgeRT.sizeDelta = new Vector2(200, 30);
            var badgeBg = _spectatorBadge.AddComponent<Image>();
            badgeBg.color = new Color(0.46f, 0.34f, 0.12f, 0.92f);
            ApplyRoundedCorners(badgeBg);
            var badgeTxtGo = CreateText(_spectatorBadge.transform, "BadgeText", Localization.Get("net.spectating"), 18,
                new Vector2(0, 0), new Vector2(200, 30));
            var badgeTxt = badgeTxtGo.GetComponent<TextMeshProUGUI>();
            TrackLocalized(badgeTxt, "net.spectating");
            badgeTxt.alignment = TextAlignmentOptions.Center;
            badgeTxt.fontStyle = FontStyles.Bold;
            var badgeTxtRT = badgeTxtGo.GetComponent<RectTransform>();
            badgeTxtRT.anchorMin = Vector2.zero;
            badgeTxtRT.anchorMax = Vector2.one;
            badgeTxtRT.offsetMin = Vector2.zero;
            badgeTxtRT.offsetMax = Vector2.zero;
            _spectatorBadge.SetActive(false);

            // Voice / video HUD bar (Agora) — hidden until session is live
            CreateAgoraCommunicationPanel();

            // Therapist-mode "Patient is acting" pointer indicator (host-side).
            // Wires its own subscription to NetworkBootstrapper.OnPatientPointerHover.
            EnsurePatientPointerOverlay();
            var sessionControls = new GameObject("SessionControlLayer", typeof(RectTransform));
            sessionControls.transform.SetParent(_sandboxUI.transform, false);
            var sessionControlsRT = (RectTransform)sessionControls.transform;
            sessionControlsRT.anchorMin = Vector2.zero; sessionControlsRT.anchorMax = Vector2.one;
            sessionControlsRT.offsetMin = sessionControlsRT.offsetMax = Vector2.zero;
            var permissionPanel = sessionControls.AddComponent<SessionControlPanel>();
            permissionPanel.Initialize(GetUIFont());
            CreateMeetingDock(permissionPanel);
            var sessionTimer = new GameObject("LiveSessionTimer", typeof(RectTransform));
            sessionTimer.transform.SetParent(_sandboxUI.transform, false);
            sessionTimer.AddComponent<LiveSessionTimer>().Initialize(GetUIFont());

            // Listen for network role changes to show/hide spectator elements
            EventBus.Subscribe<NetworkRoleAssignedEvent>(evt =>
            {
                bool isSpec = evt.Role != PlayerRole.Patient;
                _spectatorBadge?.SetActive(isSpec);
                // Editing tools remain hidden, while the catalog stays discoverable and
                // explains how to obtain editing permission when tapped.
                sandRow.SetActive(!isSpec);
                undoRedoBar.SetActive(!isSpec);
                if (brushPanel != null) brushPanel.SetActive(false);
                if (_catalogButton != null)
                    _catalogButton.SetActive(GameManager.Instance == null ||
                        GameManager.Instance.CurrentTool != ToolMode.WalkMode);
                if (isSpec && _catalogPanel != null) CloseCatalogPanel();
                // Ensure exit button stays visible for spectators
                if (exitBtnGo != null) exitBtnGo.SetActive(true);
            });

            // Update network status display
            if (NetworkBootstrapper.Instance != null)
            {
                NetworkBootstrapper.Instance.OnClientCountChanged += count =>
                {
                    if (_networkStatusText != null)
                    {
                        _networkStatusText.text = Localization.Get("net.viewers", count, count != 1 ? Localization.Get("net.viewers_s") : "");
                        _networkStatusText.gameObject.SetActive(count > 0);
                    }
                };

                // Reconnection UI hooks
                NetworkBootstrapper.Instance.OnReconnectAttempt += attempt =>
                {
                    ShowReconnectOverlay(attempt);
                };
                NetworkBootstrapper.Instance.OnReconnectGaveUp += () =>
                {
                    HideReconnectOverlay();
                    ReturnToMenu();
                };
                NetworkBootstrapper.Instance.OnConnected += () =>
                {
                    HideReconnectOverlay();
                };

                // Show room code when cloud session is created (live update)
                NetworkBootstrapper.Instance.OnRoomCreated += code =>
                {
                    UpdateRoomCodeDisplay();
                    HandleScheduledRoomCreated(code);
                };
                NetworkBootstrapper.Instance.OnConnected += () => UpdateRoomCodeDisplay();
                NetworkBootstrapper.Instance.OnConnectionError += ShowConnectionError;
                NetworkBootstrapper.Instance.OnHostingLeaseStatus += ReceiveHostingDeadline;

                // Agora: auto-join when 2+ users are in the session.
                // OnClientCountChanged fires on the host side (count = remote clients).
                // OnConnected fires on the client side (they've connected to a host = 2 people total).
                NetworkBootstrapper.Instance.OnClientCountChanged += count =>
                {
                    bool multiUser = count >= 1; // host + at least one client = 2+ people
                    if (multiUser && !AgoraManager.Instance.IsInChannel)
                    {
                        string ch = NetworkBootstrapper.Instance.RoomCode
                                    ?? System.Guid.NewGuid().ToString("N").Substring(0, 8);
                        JoinAgoraWithToken(ch);
                    }
                    else if (!multiUser)
                    {
                        LeaveAgoraSession();
                        if (_agoraPanelGo != null) _agoraPanelGo.SetActive(false);
                        if (_agoraToggleGo != null) _agoraToggleGo.SetActive(false);
                    }
                };
                NetworkBootstrapper.Instance.OnSnapshotReady += () =>
                {
                    // A client joins live media only after the complete scene is ready.
                    if (!NetworkBootstrapper.Instance.IsHost)
                    {
                        string ch = NetworkBootstrapper.Instance.RoomCode
                                    ?? System.Guid.NewGuid().ToString("N").Substring(0, 8);
                        JoinAgoraWithToken(ch);
                    }
                };
                NetworkBootstrapper.Instance.OnDisconnected += () =>
                {
                    UpdateRoomCodeDisplay();
                    LeaveAgoraSession();
                    if (_agoraPanelGo != null) _agoraPanelGo.SetActive(false);
                    if (_agoraToggleGo != null) _agoraToggleGo.SetActive(false);
                };
            }

            // === Brush Settings Panel (right of vertical toolbar) ===
            brushPanel = CreatePanel(_sandboxUI.transform, "BrushSettings",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(toolbarWidth + 8, -132), new Vector2(toolbarWidth + 508, -48));
            brushPanel.GetComponent<Image>().color = new Color(0.075f, 0.10f, 0.12f, 1f);
            // Standard UI material keeps these essential controls independent of custom shaders.
            brushPanel.GetComponent<Image>().material = null;
            brushPanel.SetActive(false);

            var radiusSlider = CreateSlider(brushPanel.transform, "RadiusSlider",
                new Vector2(10, 4), new Vector2(200, 30));
            radiusSlider.minValue = _config.MinBrushRadius;
            radiusSlider.maxValue = _config.MaxBrushRadius;
            radiusSlider.value = _config.DefaultBrushRadius;

            var radiusLabel = CreateText(brushPanel.transform, "RadiusLabel", Localization.Get("brush.radius"), 12,
                new Vector2(210, 4), new Vector2(290, 30));
            TrackLocalized(radiusLabel.GetComponent<TextMeshProUGUI>(), "brush.radius");

            var strengthSlider = CreateSlider(brushPanel.transform, "StrengthSlider",
                new Vector2(300, 4), new Vector2(490, 30));
            strengthSlider.minValue = 0.1f;
            strengthSlider.maxValue = 1f;
            strengthSlider.value = _config.DefaultBrushStrength;

            var strengthLabel = CreateText(brushPanel.transform, "StrengthLabel", Localization.Get("brush.strength"), 12,
                new Vector2(500, 4), new Vector2(580, 30));
            TrackLocalized(strengthLabel.GetComponent<TextMeshProUGUI>(), "brush.strength");

            var brushLayout = brushPanel.AddComponent<Sandplay.UI.BrushSettingsLayout>();
            brushLayout.Initialize(radiusSlider, strengthSlider,
                radiusLabel.GetComponent<TextMeshProUGUI>(), strengthLabel.GetComponent<TextMeshProUGUI>(), toolbarWidth + 8);

            // Wire sliders to sand tool
            var sandTool = FindAnyObjectByType<SandToolController>();
            if (sandTool != null)
            {
                radiusSlider.onValueChanged.AddListener(v =>
                {
                    sandTool.BrushRadius = v;
                });
                strengthSlider.onValueChanged.AddListener(v =>
                {
                    sandTool.BrushStrength = v;
                });
            }

            // Show/hide brush panel based on tool mode
            EventBus.Subscribe<ToolModeChangedEvent>(evt =>
            {
                bool isSand = evt.NewMode == ToolMode.SandRaise || evt.NewMode == ToolMode.SandDig ||
                              evt.NewMode == ToolMode.SandSmooth || evt.NewMode == ToolMode.SandFlatten ||
                              evt.NewMode == ToolMode.SandPaint;
                brushPanel.SetActive(isSand);

                // Update tool label to show current mode
                toolLabel.GetComponent<TextMeshProUGUI>().text = isSand ? evt.NewMode.ToString().Replace("Sand", "") : Localization.Get("toolbar.sand");
            });
            if (GameManager.Instance != null)
            {
                var mode = GameManager.Instance.CurrentTool;
                brushPanel.SetActive(mode == ToolMode.SandRaise || mode == ToolMode.SandDig ||
                    mode == ToolMode.SandSmooth || mode == ToolMode.SandFlatten || mode == ToolMode.SandPaint);
            }

            // === Catalog Panel (right-side drawer — API-driven) ===
            _catalogPanel = CreatePanel(_sandboxUI.transform, "CatalogPanel",
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(-420, 0), new Vector2(0, 0));
            var catalogPanelRT = _catalogPanel.GetComponent<RectTransform>();
            catalogPanelRT.pivot = new Vector2(1, 0.5f);
            catalogPanelRT.anchoredPosition = new Vector2(catalogPanelRT.rect.width, 0);
            _catalogPanel.AddComponent<Sandplay.UI.CatalogDrawerSafeAreaLayout>();
            _catalogPanel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            var catalogPanelGesture = _catalogPanel.AddComponent<Sandplay.UI.CatalogDrawerGesture>();
            catalogPanelGesture.Initialize(OpenCatalogPanel, CloseCatalogPanel);
            _catalogPanel.SetActive(false);

            // Header row: title + close button
            {
                var headerGo = new GameObject("CatalogHeader");
                headerGo.transform.SetParent(_catalogPanel.transform, false);
                var headerRT = headerGo.AddComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0, 1);
                headerRT.anchorMax = new Vector2(1, 1);
                headerRT.pivot = new Vector2(0.5f, 1);
                headerRT.anchoredPosition = Vector2.zero;
                headerRT.sizeDelta = new Vector2(0, 40);
                headerGo.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.96f);



                // Close button (×)
                var closeBtnGo = new GameObject("CloseBtn");
                closeBtnGo.transform.SetParent(headerGo.transform, false);
                var closeBtnRT = closeBtnGo.AddComponent<RectTransform>();
                closeBtnRT.anchorMin = new Vector2(0, 0.5f);
                closeBtnRT.anchorMax = new Vector2(0, 0.5f);
                closeBtnRT.pivot = new Vector2(0, 0.5f);
                closeBtnRT.anchoredPosition = new Vector2(4, 0);
                closeBtnRT.sizeDelta = new Vector2(30, 30);
                var closeBtnImg = closeBtnGo.AddComponent<Image>();
                closeBtnImg.color = new Color(0.42f, 0.22f, 0.22f, 0.92f);
                ApplyRoundedCorners(closeBtnImg);
                var closeBtn = closeBtnGo.AddComponent<Button>();
                closeBtn.onClick.AddListener(() => CloseCatalogPanel());
                var closeLbl = new GameObject("Lbl");
                closeLbl.transform.SetParent(closeBtnGo.transform, false);
                var closeTxt = closeLbl.AddComponent<TextMeshProUGUI>();
                closeTxt.text = "×";
                closeTxt.fontSize = 18;
                closeTxt.alignment = TextAlignmentOptions.Center;
                closeTxt.color = Color.white;
                closeTxt.font = GetUIFont();
                closeTxt.raycastTarget = false;
                var closeLblRT = closeLbl.GetComponent<RectTransform>();
                closeLblRT.anchorMin = Vector2.zero;
                closeLblRT.anchorMax = Vector2.one;
                closeLblRT.offsetMin = Vector2.zero;
                closeLblRT.offsetMax = Vector2.zero;
            }

            CreateCatalogCategoryStrip();

            // Status / loading text (shown while fetching)
            {
                var statusGo = new GameObject("CatalogStatus");
                statusGo.transform.SetParent(_catalogPanel.transform, false);
                _catalogStatusText = statusGo.AddComponent<TextMeshProUGUI>();
                _catalogStatusText.text = "";
                _catalogStatusText.fontSize = 12;
                _catalogStatusText.alignment = TextAlignmentOptions.Top;
                _catalogStatusText.color = new Color(0.7f, 0.7f, 0.7f);
                _catalogStatusText.font = GetUIFont();
                var statusRT = statusGo.GetComponent<RectTransform>();
                statusRT.anchorMin = new Vector2(0, 1);
                statusRT.anchorMax = new Vector2(1, 1);
                statusRT.pivot = new Vector2(0.5f, 1);
                statusRT.anchoredPosition = new Vector2(0, -80);
                statusRT.sizeDelta = new Vector2(-10, 24);
            }

            // Scroll area (items go here)
            {
                var viewportGo = new GameObject("Viewport");
                viewportGo.transform.SetParent(_catalogPanel.transform, false);
                var viewportRT = viewportGo.AddComponent<RectTransform>();
                viewportRT.anchorMin = new Vector2(0, 0);
                viewportRT.anchorMax = new Vector2(1, 1);
                viewportRT.offsetMin = new Vector2(5, 5);
                // Start the results immediately below the category strip; the
                // old loading/header reservation created a large empty band.
                viewportRT.offsetMax = new Vector2(-5, -80);
                viewportGo.AddComponent<RectMask2D>();

                _catalogContentGo = new GameObject("CatalogContent");
                _catalogContentGo.transform.SetParent(viewportGo.transform, false);
                var contentRT = _catalogContentGo.AddComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0, 1);
                contentRT.anchorMax = new Vector2(1, 1);
                contentRT.pivot = new Vector2(0.5f, 1);
                contentRT.anchoredPosition = Vector2.zero;
                contentRT.sizeDelta = Vector2.zero;

                var scrollRect = _catalogPanel.AddComponent<ScrollRect>();
                scrollRect.viewport = viewportRT;
                scrollRect.content = contentRT;
                scrollRect.vertical = true;
                scrollRect.horizontal = false;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
            }

            // === Walk Mode UI (joysticks + exit button, visible only in walk mode) ===
            var walkModeUI = new GameObject("WalkModeUI");
            walkModeUI.transform.SetParent(_sandboxUI.transform, false);
            var walkUIRT = walkModeUI.AddComponent<RectTransform>();
            walkUIRT.anchorMin = Vector2.zero;
            walkUIRT.anchorMax = Vector2.one;
            walkUIRT.offsetMin = Vector2.zero;
            walkUIRT.offsetMax = Vector2.zero;
            walkModeUI.SetActive(false);

            VirtualJoystick leftJoystick = null;

            // Only show virtual joystick on touch devices (mobile) — one joystick for movement
            // Camera look is handled by touch-drag anywhere on screen
            bool isTouchDevice = Application.platform == RuntimePlatform.IPhonePlayer
                              || Application.platform == RuntimePlatform.Android;
            if (isTouchDevice)
            {
                // -- Left joystick (movement) --
                float joystickSize = 120f;
                float joystickMargin = 30f;

                var leftJoyOuter = new GameObject("LeftJoystickOuter");
                leftJoyOuter.transform.SetParent(walkModeUI.transform, false);
                var leftOuterRT = leftJoyOuter.AddComponent<RectTransform>();
                leftOuterRT.anchorMin = new Vector2(0, 0);
                leftOuterRT.anchorMax = new Vector2(0, 0);
                leftOuterRT.pivot = new Vector2(0, 0);
                leftOuterRT.anchoredPosition = new Vector2(joystickMargin, joystickMargin);
                leftOuterRT.sizeDelta = new Vector2(joystickSize, joystickSize);
                var leftOuterImg = leftJoyOuter.AddComponent<TouchControlDisc>();
                leftOuterImg.color = new Color(.08f, .12f, .15f, .5f);

                var leftKnob = new GameObject("LeftKnob");
                leftKnob.transform.SetParent(leftJoyOuter.transform, false);
                var leftKnobRT = leftKnob.AddComponent<RectTransform>();
                leftKnobRT.anchorMin = new Vector2(0.5f, 0.5f);
                leftKnobRT.anchorMax = new Vector2(0.5f, 0.5f);
                leftKnobRT.sizeDelta = new Vector2(40f, 40f);
                var leftKnobImg = leftKnob.AddComponent<TouchControlDisc>();
                leftKnobImg.color = new Color(1f, 1f, 1f, .7f);
                leftKnobImg.raycastTarget = false;

                leftJoystick = leftJoyOuter.AddComponent<VirtualJoystick>();
                leftJoystick.Initialize(leftOuterRT, leftKnobRT);

                var jump = new GameObject("Jump", typeof(RectTransform));
                jump.transform.SetParent(walkModeUI.transform, false);
                var jumpRT = (RectTransform)jump.transform;
                jumpRT.anchorMin = jumpRT.anchorMax = new Vector2(1, 0);
                jumpRT.pivot = new Vector2(1, 0);
                jumpRT.anchoredPosition = new Vector2(-30, 40);
                jumpRT.sizeDelta = new Vector2(64, 64);
                var jumpDisc = jump.AddComponent<TouchControlDisc>();
                jumpDisc.color = new Color(.08f, .12f, .15f, .65f);
                var jumpButton = jump.AddComponent<Button>();
                jumpButton.targetGraphic = jumpDisc;
                jumpButton.onClick.AddListener(() => WalkModeController.Instance?.Jump());
                var jumpIcon = new GameObject("JumpArrow", typeof(RectTransform));
                jumpIcon.transform.SetParent(jump.transform, false);
                var jumpIconRT = (RectTransform)jumpIcon.transform;
                jumpIconRT.anchorMin = jumpIconRT.anchorMax = new Vector2(.5f, .5f);
                jumpIconRT.sizeDelta = new Vector2(28, 28);
                var jumpGlyph = jumpIcon.AddComponent<TerrainToolGlyph>();
                jumpGlyph.Mode = 0;
                jumpGlyph.color = Color.white;
                jumpGlyph.raycastTarget = false;
            }

            // -- Exit Walk button (top-right) --
            var exitWalkGo = new GameObject("Btn_ExitWalk");
            exitWalkGo.transform.SetParent(walkModeUI.transform, false);
            var exitWalkRT = exitWalkGo.AddComponent<RectTransform>();
            exitWalkRT.anchorMin = new Vector2(1, 1);
            exitWalkRT.anchorMax = new Vector2(1, 1);
            exitWalkRT.pivot = new Vector2(1, 1);
            exitWalkRT.anchoredPosition = new Vector2(-10, -10);
            exitWalkRT.sizeDelta = new Vector2(90, 44);
            var exitWalkImg = exitWalkGo.AddComponent<Image>();
            exitWalkImg.color = new Color(0.48f, 0.26f, 0.26f, 0.92f);
            ApplyRoundedCorners(exitWalkImg);
            var exitWalkBtn = exitWalkGo.AddComponent<Button>();
            exitWalkBtn.onClick.AddListener(() => GameManager.Instance.SetToolMode(ToolMode.None));
            var exitWalkTxtGo = new GameObject("Label");
            exitWalkTxtGo.transform.SetParent(exitWalkGo.transform, false);
            var exitWalkTxt = exitWalkTxtGo.AddComponent<TextMeshProUGUI>();
            exitWalkTxt.text = Localization.Get("walk.exit");
            TrackLocalized(exitWalkTxt, "walk.exit");
            exitWalkTxt.fontSize = 14;
            exitWalkTxt.alignment = TextAlignmentOptions.Center;
            exitWalkTxt.color = Color.white;
            exitWalkTxt.font = GetUIFont();
            var exitWalkTxtRT = exitWalkTxtGo.GetComponent<RectTransform>();
            exitWalkTxtRT.anchorMin = Vector2.zero;
            exitWalkTxtRT.anchorMax = Vector2.one;
            exitWalkTxtRT.offsetMin = Vector2.zero;
            exitWalkTxtRT.offsetMax = Vector2.zero;

            // Feed joystick input to WalkModeController each frame
            var joystickFeeder = walkModeUI.AddComponent<WalkJoystickFeeder>();
            joystickFeeder.Initialize(leftJoystick);

            // Wire walk mode enter/exit via ToolModeChangedEvent
            EventBus.Subscribe<ToolModeChangedEvent>(evt =>
            {
                bool entering = evt.NewMode == ToolMode.WalkMode;
                bool leaving = evt.PreviousMode == ToolMode.WalkMode && !entering;
                bool isSpec = GameManager.Instance != null && GameManager.Instance.IsSpectator;

                walkModeUI.SetActive(entering);
                sandRow.SetActive(!entering && !isSpec);
                undoRedoBar.SetActive(!entering && !isSpec);
                exitBtnGo.SetActive(!entering);
                if (entering) CloseCatalogPanel();
                _catalogButton.SetActive(!entering);

                if (entering)
                    WalkModeController.Instance?.EnterWalkMode();
                else if (leaving)
                    WalkModeController.Instance?.ExitWalkMode();
            });

            // === Catalog button (right-edge drawer toggle) ===
            _catalogPhoneLayout = IsPhoneCatalogLayout();
            _catalogButton = new GameObject("Btn_Catalog");
            _catalogButton.transform.SetParent(_sandboxUI.transform, false);
            _catalogButtonRT = _catalogButton.AddComponent<RectTransform>();
            _catalogButtonImage = _catalogButton.AddComponent<Image>();
            if (_catalogPhoneLayout)
            {
                // Phones use the compact pre-drawer control. A full-height rail
                // consumes too much of the safe-area viewport on small screens.
                _catalogButtonRT.anchorMin = new Vector2(1, 1);
                _catalogButtonRT.anchorMax = new Vector2(1, 1);
                _catalogButtonRT.pivot = new Vector2(1, 1);
                _catalogButtonRT.anchoredPosition = new Vector2(-10, -10);
                _catalogButtonRT.sizeDelta = new Vector2(80, 36);
                _catalogButtonImage.color = new Color(0.22f, 0.42f, 0.52f, 0.92f);
                ApplyRoundedCorners(_catalogButtonImage);
            }
            else
            {
                _catalogButtonRT.anchorMin = new Vector2(1, 0);
                _catalogButtonRT.anchorMax = new Vector2(1, 1);
                _catalogButtonRT.pivot = new Vector2(1, 0.5f);
                // Sit flush against the right edge; the grab handle protrudes
                // from the opposite side like a conventional drawer pull.
                _catalogButtonRT.anchoredPosition = Vector2.zero;
                _catalogButtonRT.sizeDelta = new Vector2(56, 0);
                _catalogButtonImage.color = new Color(0.045f, 0.055f, 0.06f, 1f);
            }
            catalogBtn = _catalogButton.AddComponent<Button>();
            catalogBtn.onClick.AddListener(ToggleCatalogPanel);
            var catalogGesture = _catalogButton.AddComponent<Sandplay.UI.CatalogDrawerGesture>();
            catalogGesture.Initialize(OpenCatalogPanel, CloseCatalogPanel);
            var catalogHint = _catalogButton.AddComponent<Sandplay.UI.CatalogButtonHint>();
            catalogHint.Initialize(_catalogButtonImage);
            // The first use teaches the affordance, then the cue stays out of the way.
            catalogBtn.onClick.AddListener(catalogHint.Dismiss);

            var catalogHandleGo = new GameObject("DrawerHandle");
            catalogHandleGo.transform.SetParent(_catalogButton.transform, false);
            _catalogButtonHandleVisual = catalogHandleGo;
            var catalogHandleImg = catalogHandleGo.AddComponent<Image>();
            catalogHandleImg.color = new Color(0.015f, 0.02f, 0.025f, 1f);
            catalogHandleImg.raycastTarget = false;
            ApplyRoundedCorners(catalogHandleImg);
            var catalogHandleRT = catalogHandleGo.GetComponent<RectTransform>();
            catalogHandleRT.anchorMin = new Vector2(0, 0.5f);
            catalogHandleRT.anchorMax = new Vector2(0, 0.5f);
            catalogHandleRT.pivot = new Vector2(1, 0.5f);
            catalogHandleRT.anchoredPosition = Vector2.zero;
            catalogHandleRT.sizeDelta = new Vector2(14, 150);
            if (_catalogPhoneLayout)
                catalogHandleGo.SetActive(false);

            var catalogIcon = LoadIconWhiteTinted("catalog");
            if (catalogIcon != null)
            {
                var catalogIconGo = new GameObject("Icon");
                catalogIconGo.transform.SetParent(_catalogButton.transform, false);
                _catalogButtonGlyph = catalogIconGo;
                var catalogIconImg = catalogIconGo.AddComponent<Image>();
                catalogIconImg.sprite = catalogIcon;
                catalogIconImg.preserveAspect = true;
                catalogIconImg.raycastTarget = false;
                var catalogIconRT = catalogIconGo.GetComponent<RectTransform>();
                if (_catalogPhoneLayout)
                {
                    catalogIconRT.anchorMin = new Vector2(.15f, .15f);
                    catalogIconRT.anchorMax = new Vector2(.85f, .85f);
                    catalogIconRT.offsetMin = Vector2.zero;
                    catalogIconRT.offsetMax = Vector2.zero;
                }
                else
                {
                    catalogIconRT.anchorMin = catalogIconRT.anchorMax = new Vector2(0.5f, 0.5f);
                    catalogIconRT.sizeDelta = new Vector2(30, 30);
                    catalogIconRT.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                var catalogLabel = new GameObject("Label");
                catalogLabel.transform.SetParent(_catalogButton.transform, false);
                _catalogButtonGlyph = catalogLabel;
                var catalogText = catalogLabel.AddComponent<TextMeshProUGUI>();
                catalogText.text = Localization.Get("button.catalog");
                TrackLocalized(catalogText, "button.catalog");
                catalogText.fontSize = 11;
                catalogText.alignment = TextAlignmentOptions.Center;
                catalogText.color = Color.white;
                catalogText.font = GetUIFont();
                catalogText.raycastTarget = false;
                var catalogLabelRT = catalogLabel.GetComponent<RectTransform>();
                catalogLabelRT.anchorMin = Vector2.zero;
                catalogLabelRT.anchorMax = Vector2.one;
                catalogLabelRT.offsetMin = catalogLabelRT.offsetMax = Vector2.zero;
            }

            // === Status bar (bottom, right of toolbar) ===
            var statusBar = CreatePanel(_sandboxUI.transform, "StatusBar",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(toolbarWidth, 25), new Vector2(0, 0));
            statusBar.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            var statusText = CreateText(statusBar.transform, "StatusText", Localization.Get("status.hint"), 12,
                new Vector2(10, 0), new Vector2(800, 25));
            statusText.AddComponent<AutoSaveStatusLabel>()
                .Initialize(statusText.GetComponent<TextMeshProUGUI>());

            var saveIndicator = new GameObject("BoardSaveIndicator", typeof(RectTransform));
            saveIndicator.transform.SetParent(_sandboxUI.transform, false);
            saveIndicator.AddComponent<BoardSaveIndicator>().Initialize(GetUIFont());

            // === Floating object toolbar (Up/Down, Rotate, Resize, Duplicate, Delete) ===
            var actionPanelGo = new GameObject("ObjectActionPanel");
            actionPanelGo.transform.SetParent(_sandboxUI.transform, false);
            var actionRT = actionPanelGo.AddComponent<RectTransform>();
            actionRT.sizeDelta = new Vector2(310, 56);
            // Scale the complete strip, including icons, spacing and shortcut labels.
            actionRT.localScale = Vector3.one * 0.81f;
            actionRT.pivot = new Vector2(0.5f, 0f);

            var actionBg = actionPanelGo.AddComponent<Image>();
            actionBg.color = new Color(0.08f, 0.10f, 0.14f, 0.94f);
            ApplyRoundedCorners(actionBg);

            var actionLayout = actionPanelGo.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 6;
            actionLayout.padding = new RectOffset(5, 5, 5, 5);
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = true;

            var actionPanel = actionPanelGo.AddComponent<Sandplay.UI.ObjectActionPanel>();

            // Up/down button — drag vertically to raise or sink the selected object.
            var verticalBtnGo = new GameObject("Btn_Vertical");
            verticalBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var verticalBtnImg = verticalBtnGo.AddComponent<Image>();
            verticalBtnImg.color = new Color(0.32f, 0.30f, 0.52f, 1f);
            ApplyRoundedCorners(verticalBtnImg);
            var verticalBtn = verticalBtnGo.AddComponent<Button>();
            verticalBtn.targetGraphic = verticalBtnImg;
            var verticalColors = verticalBtn.colors;
            verticalColors.highlightedColor = new Color(0.44f, 0.42f, 0.68f, 1f);
            verticalColors.pressedColor = new Color(0.24f, 0.22f, 0.44f, 1f);
            verticalBtn.colors = verticalColors;
            Sprite verticalIcon = LoadIconWhiteTinted("vertical");
            if (verticalIcon != null)
            {
                var verticalIconGo = new GameObject("Icon");
                verticalIconGo.transform.SetParent(verticalBtnGo.transform, false);
                var verticalIconImg = verticalIconGo.AddComponent<Image>();
                verticalIconImg.sprite = verticalIcon;
                verticalIconImg.preserveAspect = true;
                verticalIconImg.raycastTarget = false;
                var verticalIconRT = verticalIconGo.GetComponent<RectTransform>();
                verticalIconRT.anchorMin = new Vector2(0.16f, 0.16f);
                verticalIconRT.anchorMax = new Vector2(0.84f, 0.84f);
                verticalIconRT.offsetMin = Vector2.zero;
                verticalIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var verticalTxtGo = new GameObject("Label");
                verticalTxtGo.transform.SetParent(verticalBtnGo.transform, false);
                var verticalTxt = verticalTxtGo.AddComponent<TextMeshProUGUI>();
                verticalTxt.text = Localization.Get("action.vertical");
                verticalTxt.fontSize = 25;
                verticalTxt.alignment = TextAlignmentOptions.Center;
                verticalTxt.color = Color.white;
                verticalTxt.font = GetUIFont();
                verticalTxt.raycastTarget = false;
                var verticalTxtRT = verticalTxtGo.GetComponent<RectTransform>();
                verticalTxtRT.anchorMin = Vector2.zero;
                verticalTxtRT.anchorMax = Vector2.one;
                verticalTxtRT.offsetMin = Vector2.zero;
                verticalTxtRT.offsetMax = Vector2.zero;
            }
            var verticalTrigger = verticalBtnGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var verticalDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            verticalDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            verticalDown.callback.AddListener((_) => actionPanel.OnVerticalPointerDown());
            verticalTrigger.triggers.Add(verticalDown);
            var verticalUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            verticalUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            verticalUp.callback.AddListener((_) => actionPanel.OnVerticalPointerUp());
            verticalTrigger.triggers.Add(verticalUp);

            // Rotate button
            var rotateBtnGo = new GameObject("Btn_Rotate");
            rotateBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var rotateBtnImg = rotateBtnGo.AddComponent<Image>();
            rotateBtnImg.color = new Color(0.20f, 0.40f, 0.54f, 1f);
            ApplyRoundedCorners(rotateBtnImg);
            var rotateBtn = rotateBtnGo.AddComponent<Button>();
            rotateBtn.targetGraphic = rotateBtnImg;
            var rotateColors = rotateBtn.colors;
            rotateColors.highlightedColor = new Color(0.28f, 0.54f, 0.70f, 1f);
            rotateColors.pressedColor = new Color(0.14f, 0.31f, 0.46f, 1f);
            rotateBtn.colors = rotateColors;
            Sprite rotateIcon = LoadIconWhiteTinted("rotate");
            if (rotateIcon != null)
            {
                var rotateIconGo = new GameObject("Icon");
                rotateIconGo.transform.SetParent(rotateBtnGo.transform, false);
                var rotateIconImg = rotateIconGo.AddComponent<Image>();
                rotateIconImg.sprite = rotateIcon;
                rotateIconImg.preserveAspect = true;
                rotateIconImg.raycastTarget = false;
                var rotateIconRT = rotateIconGo.GetComponent<RectTransform>();
                rotateIconRT.anchorMin = new Vector2(0.1f, 0.1f);
                rotateIconRT.anchorMax = new Vector2(0.9f, 0.9f);
                rotateIconRT.offsetMin = Vector2.zero;
                rotateIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var rotateTxtGo = new GameObject("Label");
                rotateTxtGo.transform.SetParent(rotateBtnGo.transform, false);
                var rotateTxt = rotateTxtGo.AddComponent<TextMeshProUGUI>();
                rotateTxt.text = "\u21BB";
                rotateTxt.fontSize = 20;
                rotateTxt.alignment = TextAlignmentOptions.Center;
                rotateTxt.color = Color.white;
                rotateTxt.font = GetUIFont();
                rotateTxt.raycastTarget = false;
                var rotateTxtRT = rotateTxtGo.GetComponent<RectTransform>();
                rotateTxtRT.anchorMin = Vector2.zero;
                rotateTxtRT.anchorMax = Vector2.one;
                rotateTxtRT.offsetMin = Vector2.zero;
                rotateTxtRT.offsetMax = Vector2.zero;
            }
            // Drag-to-rotate via EventTrigger
            var rotateTrigger = rotateBtnGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((_) => actionPanel.OnRotatePointerDown());
            rotateTrigger.triggers.Add(pointerDown);
            var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((_) => actionPanel.OnRotatePointerUp());
            rotateTrigger.triggers.Add(pointerUp);

            // Resize button
            var resizeBtnGo = new GameObject("Btn_Resize");
            resizeBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var resizeBtnImg = resizeBtnGo.AddComponent<Image>();
            resizeBtnImg.color = new Color(0.22f, 0.44f, 0.34f, 1f);
            ApplyRoundedCorners(resizeBtnImg);
            var resizeBtn = resizeBtnGo.AddComponent<Button>();
            resizeBtn.targetGraphic = resizeBtnImg;
            var resizeColors = resizeBtn.colors;
            resizeColors.highlightedColor = new Color(0.30f, 0.58f, 0.45f, 1f);
            resizeColors.pressedColor = new Color(0.16f, 0.34f, 0.26f, 1f);
            resizeBtn.colors = resizeColors;
            Sprite resizeIcon = LoadIconWhiteTinted("resize");
            if (resizeIcon != null)
            {
                var resizeIconGo = new GameObject("Icon");
                resizeIconGo.transform.SetParent(resizeBtnGo.transform, false);
                var resizeIconImg = resizeIconGo.AddComponent<Image>();
                resizeIconImg.sprite = resizeIcon;
                resizeIconImg.preserveAspect = true;
                resizeIconImg.raycastTarget = false;
                var resizeIconRT = resizeIconGo.GetComponent<RectTransform>();
                resizeIconRT.anchorMin = new Vector2(0.1f, 0.1f);
                resizeIconRT.anchorMax = new Vector2(0.9f, 0.9f);
                resizeIconRT.offsetMin = Vector2.zero;
                resizeIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var resizeTxtGo = new GameObject("Label");
                resizeTxtGo.transform.SetParent(resizeBtnGo.transform, false);
                var resizeTxt = resizeTxtGo.AddComponent<TextMeshProUGUI>();
                resizeTxt.text = "\u2922";
                resizeTxt.fontSize = 20;
                resizeTxt.alignment = TextAlignmentOptions.Center;
                resizeTxt.color = Color.white;
                resizeTxt.font = GetUIFont();
                resizeTxt.raycastTarget = false;
                var resizeTxtRT = resizeTxtGo.GetComponent<RectTransform>();
                resizeTxtRT.anchorMin = Vector2.zero;
                resizeTxtRT.anchorMax = Vector2.one;
                resizeTxtRT.offsetMin = Vector2.zero;
                resizeTxtRT.offsetMax = Vector2.zero;
            }
            var resizeTrigger = resizeBtnGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var resizeDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            resizeDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            resizeDown.callback.AddListener((_) => actionPanel.OnResizePointerDown());
            resizeTrigger.triggers.Add(resizeDown);
            var resizeUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            resizeUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            resizeUp.callback.AddListener((_) => actionPanel.OnResizePointerUp());
            resizeTrigger.triggers.Add(resizeUp);

            // Duplicate button — places an identical copy beside the selected object.
            var duplicateBtnGo = new GameObject("Btn_Duplicate");
            duplicateBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var duplicateBtnImg = duplicateBtnGo.AddComponent<Image>();
            duplicateBtnImg.color = new Color(0.43f, 0.34f, 0.58f, 1f);
            ApplyRoundedCorners(duplicateBtnImg);
            var duplicateBtn = duplicateBtnGo.AddComponent<Button>();
            duplicateBtn.targetGraphic = duplicateBtnImg;
            var duplicateColors = duplicateBtn.colors;
            duplicateColors.highlightedColor = new Color(0.56f, 0.46f, 0.73f, 1f);
            duplicateColors.pressedColor = new Color(0.32f, 0.24f, 0.47f, 1f);
            duplicateBtn.colors = duplicateColors;
            Sprite duplicateIcon = LoadIconWhiteTinted("copy");
            if (duplicateIcon != null)
            {
                var duplicateIconGo = new GameObject("Icon");
                duplicateIconGo.transform.SetParent(duplicateBtnGo.transform, false);
                var duplicateIconImg = duplicateIconGo.AddComponent<Image>();
                duplicateIconImg.sprite = duplicateIcon;
                duplicateIconImg.preserveAspect = true;
                duplicateIconImg.raycastTarget = false;
                var duplicateIconRT = duplicateIconGo.GetComponent<RectTransform>();
                duplicateIconRT.anchorMin = new Vector2(0.14f, 0.14f);
                duplicateIconRT.anchorMax = new Vector2(0.86f, 0.86f);
                duplicateIconRT.offsetMin = Vector2.zero;
                duplicateIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var duplicateTxtGo = new GameObject("Label");
                duplicateTxtGo.transform.SetParent(duplicateBtnGo.transform, false);
                var duplicateTxt = duplicateTxtGo.AddComponent<TextMeshProUGUI>();
                duplicateTxt.text = Localization.Get("action.duplicate");
                duplicateTxt.fontSize = 11;
                duplicateTxt.enableAutoSizing = true;
                duplicateTxt.fontSizeMin = 8;
                duplicateTxt.fontSizeMax = 11;
                duplicateTxt.alignment = TextAlignmentOptions.Center;
                duplicateTxt.color = Color.white;
                duplicateTxt.font = GetUIFont();
                duplicateTxt.raycastTarget = false;
                var duplicateTxtRT = duplicateTxtGo.GetComponent<RectTransform>();
                duplicateTxtRT.anchorMin = Vector2.zero;
                duplicateTxtRT.anchorMax = Vector2.one;
                duplicateTxtRT.offsetMin = new Vector2(2, 2);
                duplicateTxtRT.offsetMax = new Vector2(-2, -2);
            }
            duplicateBtn.onClick.AddListener(() => actionPanel.OnDuplicatePressed());

            // Delete button
            var deleteBtnGo = new GameObject("Btn_Delete");
            deleteBtnGo.transform.SetParent(actionPanelGo.transform, false);
            var deleteBtnImg = deleteBtnGo.AddComponent<Image>();
            deleteBtnImg.color = new Color(0.48f, 0.26f, 0.26f, 0.92f);
            ApplyRoundedCorners(deleteBtnImg);
            var deleteBtn = deleteBtnGo.AddComponent<Button>();
            deleteBtn.targetGraphic = deleteBtnImg;
            var deleteColors = deleteBtn.colors;
            deleteColors.highlightedColor = new Color(0.62f, 0.34f, 0.34f, 1f);
            deleteColors.pressedColor = new Color(0.38f, 0.18f, 0.18f, 1f);
            deleteBtn.colors = deleteColors;
            Sprite deleteIcon = LoadIconWhiteTinted("delete");
            if (deleteIcon != null)
            {
                var deleteIconGo = new GameObject("Icon");
                deleteIconGo.transform.SetParent(deleteBtnGo.transform, false);
                var deleteIconImg = deleteIconGo.AddComponent<Image>();
                deleteIconImg.sprite = deleteIcon;
                deleteIconImg.preserveAspect = true;
                deleteIconImg.raycastTarget = false;
                var deleteIconRT = deleteIconGo.GetComponent<RectTransform>();
                deleteIconRT.anchorMin = new Vector2(0.1f, 0.1f);
                deleteIconRT.anchorMax = new Vector2(0.9f, 0.9f);
                deleteIconRT.offsetMin = Vector2.zero;
                deleteIconRT.offsetMax = Vector2.zero;
            }
            else
            {
                var deleteTxtGo = new GameObject("Label");
                deleteTxtGo.transform.SetParent(deleteBtnGo.transform, false);
                var deleteTxt = deleteTxtGo.AddComponent<TextMeshProUGUI>();
                deleteTxt.text = "\u2716";
                deleteTxt.fontSize = 18;
                deleteTxt.alignment = TextAlignmentOptions.Center;
                deleteTxt.color = Color.white;
                deleteTxt.font = GetUIFont();
                deleteTxt.raycastTarget = false;
                var deleteTxtRT = deleteTxtGo.GetComponent<RectTransform>();
                deleteTxtRT.anchorMin = Vector2.zero;
                deleteTxtRT.anchorMax = Vector2.one;
                deleteTxtRT.offsetMin = Vector2.zero;
                deleteTxtRT.offsetMax = Vector2.zero;
            }
            deleteBtn.onClick.AddListener(() => actionPanel.OnDeletePressed());

            actionPanel.ConfigureKeyboardHints(GetUIFont());
            actionPanel.Initialize(UnityEngine.Camera.main);

            // === AI-Assisted Reflection Panel ===
            CreateAnalysisPanel();
        }


        // ======================= Main Menu =======================


        private Button CreateMenuButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true; // Ensure button can receive clicks
            ApplyRoundedCorners(img);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img; // Explicitly set the target graphic
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 16;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.font = GetUIFont();
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return btn;
        }


        private RenderTexture RenderObjectThumbnail(GameObject prefab, int width, int height)
        {
            if (prefab == null) return null;

            // Create a temporary clone for rendering
            var clone = Instantiate(prefab);
            clone.SetActive(true);
            clone.transform.position = new Vector3(1000, 1000, 1000); // far away from scene

            // Use isolated layer
            int layer = 31;
            foreach (var t in clone.GetComponentsInChildren<Transform>())
                t.gameObject.layer = layer;

            // Measure bounds
            var renderers = clone.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(clone);
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            // Set up camera to frame the object
            var camGo = new GameObject("ThumbnailCam");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0f);
            cam.orthographic = true;
            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            cam.orthographicSize = maxExtent * 1.2f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = maxExtent * 10f;

            // Position camera at a 3/4 angle looking at the object
            Vector3 center = bounds.center;
            Vector3 offset = new Vector3(1, 0.8f, 1).normalized * maxExtent * 3f;
            camGo.transform.position = center + offset;
            camGo.transform.LookAt(center);

            // Add a temporary light
            var lightGo = new GameObject("ThumbnailLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45, 45, 0);

            // Render to texture
            var rt = new RenderTexture(width, height, 16);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            // Cleanup
            Destroy(camGo);
            Destroy(lightGo);
            Destroy(clone);

            return rt;
        }

        // --- UI Helper Methods ---

        private static TMP_FontAsset _uiFont;
        private static TMP_FontAsset _cjkFont;
        private static bool _cjkFallbackAdded;
        private static TMP_FontAsset _japaneseFont;
        private static TMP_FontAsset _latinFallbackFont;

        /// <summary>Latin UI font with Chinese fallback attached.</summary>
        private static TMP_FontAsset GetUIFont()
        {
            EnsureCjkFallback();
            if (Localization.Current == Language.Japanese)
            {
                if (_japaneseFont == null)
                    _japaneseFont = TryCreateDynamicCjkTmpFont(Resources.Load<Font>("Fonts/NotoSansCJKjp-Regular"));
                if (_japaneseFont != null) return _japaneseFont;
            }
            return _uiFont;
        }

        /// <summary>
        /// Full Simplified Chinese TMP font (dynamic multi-atlas). Prefer for long AI reports.
        /// </summary>
        private static TMP_FontAsset GetCJKFont()
        {
            EnsureCjkFallback();
            return _cjkFont != null ? _cjkFont : _uiFont;
        }

        private static void EnsureCjkFallback()
        {
            if (_uiFont == null)
                _uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // The bundled static atlas is mostly ASCII. Bundle the font source so
            // Czech accents (including the language selector) work on every OS.
            if (_uiFont != null && _latinFallbackFont == null)
            {
                _latinFallbackFont = TryCreateDynamicCjkTmpFont(Resources.Load<Font>("Fonts/LiberationSans"));
                if (_latinFallbackFont != null)
                {
                    if (_uiFont.fallbackFontAssetTable == null)
                        _uiFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    _uiFont.fallbackFontAssetTable.Insert(0, _latinFallbackFont);
                }
            }

            if (_cjkFallbackAdded || _uiFont == null) return;

            // Pre-baked TMP asset if present; else build dynamic from full NotoSansSC OTF.
            _cjkFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC SDF");

            if (_cjkFont == null)
            {
                Debug.LogWarning("[SceneBootstrapper] NotoSansSC SDF not found. Creating dynamic CJK font from bundled OTF…");

                // Full NotoSansSC first (correct fix). STHeiti is a tiny UI subset — last resort only.
                string[] bundledPaths = { "Fonts/NotoSansSC-Regular", "Fonts/STHeiti-Medium" };
                foreach (var fontPath in bundledPaths)
                {
                    var bundled = Resources.Load<Font>(fontPath);
                    _cjkFont = TryCreateDynamicCjkTmpFont(bundled);
                    if (_cjkFont != null)
                    {
                        Debug.Log($"[SceneBootstrapper] Dynamic CJK font from bundled: {fontPath}");
                        break;
                    }
                }

                if (_cjkFont == null)
                {
                    string[] osFontNames =
                    {
                        "Hiragino Sans GB", "Heiti SC", "STHeiti",
                        "Microsoft YaHei", "Microsoft YaHei UI", "SimHei",
                        "Noto Sans CJK SC", "Source Han Sans SC", "Noto Sans SC"
                    };
                    foreach (var name in osFontNames)
                    {
                        Font osFont = null;
                        try { osFont = Font.CreateDynamicFontFromOSFont(name, 72); }
                        catch { /* try next */ }
                        _cjkFont = TryCreateDynamicCjkTmpFont(osFont);
                        if (_cjkFont != null)
                        {
                            Debug.Log($"[SceneBootstrapper] Dynamic CJK font from OS: {name}");
                            break;
                        }
                    }
                }

                if (_cjkFont == null)
                {
                    Debug.LogError("[SceneBootstrapper] Could not create Chinese font. " +
                        "Expected Resources/Fonts/NotoSansSC-Regular.otf (full CJK).");
                }
            }

            if (_cjkFont != null)
            {
                if (_uiFont.fallbackFontAssetTable == null)
                    _uiFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (!_uiFont.fallbackFontAssetTable.Contains(_cjkFont))
                    _uiFont.fallbackFontAssetTable.Add(_cjkFont);
                Debug.Log($"[SceneBootstrapper] Added Chinese fallback font: {_cjkFont.name}");
            }
            else
            {
                Debug.LogError("[SceneBootstrapper] Failed to load Chinese font. Chinese text will show as squares.");
            }

            _cjkFallbackAdded = true;
        }

        private static TMP_FontAsset TryCreateDynamicCjkTmpFont(Font sourceFont)
        {
            if (sourceFont == null) return null;
            try
            {
                var asset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    72,
                    6,
                    GlyphRenderMode.SDFAA,
                    2048,
                    2048,
                    AtlasPopulationMode.Dynamic
                );
                if (asset == null) return null;
                // Long AI reports need many unique glyphs — allow extra atlases.
                asset.isMultiAtlasTexturesEnabled = true;
                return asset;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SceneBootstrapper] TMP CreateFontAsset failed for {sourceFont.name}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Apply rounded corner material to an Image component.
        /// </summary>
        private void ApplyRoundedCorners(Image img)
        {
            if (_roundedUIMaterial != null && img != null)
            {
                img.material = _roundedUIMaterial;
            }
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.14f, 0.16f, 0.9f);
            ApplyRoundedCorners(img);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        /// <summary>
        /// Load an icon PNG from Resources/UI/ and tint all non-transparent pixels white.
        /// Uses RenderTexture blit because Resources.Load textures aren't CPU-readable.
        /// </summary>
        private Sprite LoadIconWhiteTinted(string label)
        {
            // Map label to resource name (handle multi-line labels like "AI\nAnalysis")
            string resourceName = label.Replace("\n", " ");
            var tex = Resources.Load<Texture2D>("UI/" + resourceName);
            if (tex == null) return null;

            // Blit through RenderTexture to get a CPU-readable copy
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // Tint all non-transparent pixels white
            var pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.01f)
                    pixels[i] = new Color(1f, 1f, 1f, pixels[i].a);
            }
            readable.SetPixels(pixels);
            readable.Apply();

            return Sprite.Create(readable, new Rect(0, 0, readable.width, readable.height),
                new Vector2(0.5f, 0.5f));
        }

        private Sprite LoadIconRaw(string label)
        {
            string resourceName = label.Replace("\n", " ");
            var tex = Resources.Load<Texture2D>("UI/" + resourceName);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private Button CreateVerticalToolButton(Transform parent, string name, string label,
            float toolbarW, float btnH, ref float yOffset, float pad)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            ApplyRoundedCorners(img);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.45f, 0.45f, 0.55f);
            colors.pressedColor = new Color(0.25f, 0.4f, 0.6f);
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            float margin = 4f;
            rt.anchoredPosition = new Vector2(margin, yOffset);
            rt.sizeDelta = new Vector2(toolbarW - margin * 2, btnH);

            // Try to load icon from Resources/UI/<label>.png and tint white
            Sprite iconSprite = LoadIconWhiteTinted(label);
            if (iconSprite != null)
            {
                // Icon in upper portion
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                var iconRT = iconGo.GetComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.1f, 0.28f);
                iconRT.anchorMax = new Vector2(0.9f, 0.95f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;

                // Label below icon
                var textGo = new GameObject("Label");
                textGo.transform.SetParent(go.transform, false);
                var txt = textGo.AddComponent<TextMeshProUGUI>();
                txt.text = label.Replace("\n", " ");
                txt.fontSize = 9;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = new Color(1f, 1f, 1f, 0.85f);
                txt.font = GetUIFont();
                txt.raycastTarget = false;
                var textRT = textGo.GetComponent<RectTransform>();
                textRT.anchorMin = new Vector2(0, 0);
                textRT.anchorMax = new Vector2(1, 0.28f);
                textRT.offsetMin = Vector2.zero;
                textRT.offsetMax = Vector2.zero;
            }
            else
            {
                var textGo = new GameObject("Label");
                textGo.transform.SetParent(go.transform, false);
                var txt = textGo.AddComponent<TextMeshProUGUI>();
                txt.text = label;
                txt.fontSize = 11;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.white;
                txt.font = GetUIFont();
                var textRT = textGo.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = Vector2.zero;
                textRT.offsetMax = Vector2.zero;
            }

            var oldIcon = go.transform.Find("Icon");
            if (oldIcon != null) oldIcon.gameObject.SetActive(false);
            string key = name.Contains("Settings") ? "settings" : name.Contains("Analyze") ? "ai" :
                name.Contains("Manual") ? "replays" : name.Contains("Walk") ? "multiplayer" :
                name.Contains("Material") ? "objects" : "new_board";
            var modernIcon = AddHomeIconGraphic(go.transform, key, new Vector2(.3f,.4f), new Vector2(.7f,.88f), new Color(.84f,.89f,.91f));
            if (name.Contains("Material") || name.Contains("Walk") || name.Contains("Manual"))
            {
                modernIcon.enabled = false;
                var customGo = new GameObject("ActionGlyph", typeof(RectTransform));
                customGo.transform.SetParent(go.transform, false);
                var customRT = customGo.GetComponent<RectTransform>();
                customRT.anchorMin = customRT.anchorMax = new Vector2(.5f,.65f);
                customRT.sizeDelta = new Vector2(22,22);
                var custom = customGo.AddComponent<TerrainToolGlyph>();
                custom.Mode = name.Contains("Material") ? 3 : name.Contains("Walk") ? 4 : 5;
                custom.color = new Color(.84f,.89f,.91f);
                custom.raycastTarget = false;
            }
            var modernLabel = go.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            modernLabel.enableAutoSizing = true;
            modernLabel.fontSizeMin = 7;
            modernLabel.fontSizeMax = 10;
            modernLabel.enableWordWrapping = true;
            modernLabel.overflowMode = TextOverflowModes.Ellipsis;
            modernLabel.rectTransform.anchorMin = new Vector2(.03f,.03f);
            modernLabel.rectTransform.anchorMax = new Vector2(.97f,.4f);
            img.color = new Color(.12f,.15f,.17f);
            colors.highlightedColor = new Color(1.3f,1.3f,1.3f);
            colors.pressedColor = new Color(.7f,.85f,.9f);
            btn.colors = colors;
            yOffset -= (btnH + pad);
            return btn;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 posMin, Vector2 posMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            ApplyRoundedCorners(img);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.45f, 0.45f, 0.55f);
            colors.pressedColor = new Color(0.25f, 0.4f, 0.6f);
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = (posMin + posMax) * 0.5f;
            rt.sizeDelta = posMax - posMin;

            // Label
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 14;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.font = GetUIFont();
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return btn;
        }

        private GameObject CreateText(Transform parent, string name, string text, int fontSize,
            Vector2 posMin, Vector2 posMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.TopLeft;
            txt.richText = true;
            txt.font = GetUIFont();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.anchoredPosition = (posMin + posMax) * 0.5f;
            rt.sizeDelta = new Vector2(Mathf.Abs(posMax.x - posMin.x), Mathf.Abs(posMax.y - posMin.y));

            return go;
        }

        private Slider CreateSlider(Transform parent, string name, Vector2 posMin, Vector2 posMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = (posMin + posMax) * 0.5f;
            rt.sizeDelta = posMax - posMin;

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f);
            ApplyRoundedCorners(bgImg);
            var bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.35f);
            bgRT.anchorMax = new Vector2(1, 0.65f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.35f);
            fillAreaRT.anchorMax = new Vector2(1, 0.65f);
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillArea.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.4f, 0.6f, 0.9f);
            ApplyRoundedCorners(fillImg);
            var fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10, 0);
            handleAreaRT.offsetMax = new Vector2(-10, 0);

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleArea.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            ApplyRoundedCorners(handleImg);
            var handleRT = handleGo.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(16, 0);
            handleRT.anchorMin = new Vector2(0, 0);
            handleRT.anchorMax = new Vector2(0, 1);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

    }
}
