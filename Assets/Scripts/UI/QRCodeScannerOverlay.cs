using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    /// <summary>
    /// Full-screen camera overlay that scans for QR codes.
    /// On a real iOS device this delegates to the native AVFoundation scanner
    /// (same engine as the iPhone Camera app) for reliable detection.
    /// On Android / Editor it falls back to WebCamTexture + the bundled decoder.
    /// Fires OnCodeScanned when found.
    /// </summary>
    public class QRCodeScannerOverlay : MonoBehaviour
    {
        public System.Action<string> OnCodeScanned;

        private WebCamTexture _webcam;
        private RawImage _preview;
        private Text _statusText;
        private float _scanInterval = 0.3f;
        private float _nextScan;
        private Texture2D _snapTex;
        private bool _found;
        private bool _waitingForPermission;
        private float _permissionRetryTime;
        private bool _previewSized;
        private RectTransform _viewfinderRT;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _SandplayQR_Start(string callbackObject, string callbackMethod);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _SandplayQR_Stop();
#endif

        public void Initialize()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // On iOS device, use the native AVFoundation scanner. We keep this
            // GameObject alive (invisible) so UnitySendMessage can reach us.
            gameObject.name = "SandplayQRScanner_" + GetInstanceID();
            _SandplayQR_Start(gameObject.name, nameof(_OnNativeQRResult));
            return;
#else
            BuildOverlayUI();
#endif
        }

        private void BuildOverlayUI()
        {
            // Full screen container
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Black background (visible before camera starts)
            var bg = gameObject.AddComponent<Image>();
            bg.color = Color.black;

            // Camera preview - full screen, will be scaled to cover
            var previewGo = new GameObject("CameraPreview");
            previewGo.transform.SetParent(transform, false);
            _preview = previewGo.AddComponent<RawImage>();
            _preview.color = Color.white;
            var prevRT = previewGo.GetComponent<RectTransform>();
            prevRT.anchorMin = new Vector2(0.5f, 0.5f);
            prevRT.anchorMax = new Vector2(0.5f, 0.5f);
            prevRT.pivot = new Vector2(0.5f, 0.5f);
            prevRT.anchoredPosition = Vector2.zero;
            prevRT.sizeDelta = new Vector2(Screen.width, Screen.height);

            // Dark overlay masks (4 rects around the viewfinder cutout)
            float vfSide = Mathf.Min(Screen.width, Screen.height) * 0.65f;
            Color maskColor = new Color(0, 0, 0, 0.6f);

            // Viewfinder container (for corner brackets positioning)
            var vfGo = new GameObject("Viewfinder");
            vfGo.transform.SetParent(transform, false);
            _viewfinderRT = vfGo.AddComponent<RectTransform>();
            _viewfinderRT.anchorMin = new Vector2(0.5f, 0.5f);
            _viewfinderRT.anchorMax = new Vector2(0.5f, 0.5f);
            _viewfinderRT.pivot = new Vector2(0.5f, 0.5f);
            _viewfinderRT.anchoredPosition = Vector2.zero;
            _viewfinderRT.sizeDelta = new Vector2(vfSide, vfSide);

            // Top mask
            CreateMask("MaskTop", new Vector2(0, 0.5f), new Vector2(1, 1),
                new Vector2(0, vfSide / 2f), Vector2.zero, maskColor);
            // Bottom mask
            CreateMask("MaskBottom", new Vector2(0, 0), new Vector2(1, 0.5f),
                Vector2.zero, new Vector2(0, -vfSide / 2f), maskColor);
            // Left mask
            CreateMask("MaskLeft", new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -vfSide / 2f), new Vector2(-vfSide / 2f, vfSide / 2f), maskColor);
            // Right mask
            CreateMask("MaskRight", new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f),
                new Vector2(vfSide / 2f, -vfSide / 2f), new Vector2(0, vfSide / 2f), maskColor);

            // Corner brackets on the viewfinder
            float bracketLen = 0.18f;
            float bracketThick = 3f;
            Color bracketColor = new Color(0.3f, 0.8f, 1f);
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                true, 0, 1, bracketLen);       // top-left H
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                false, 0, 1, bracketLen);      // top-left V
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                true, 1, 1, bracketLen);       // top-right H
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                false, 1, 1, bracketLen);      // top-right V
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                true, 0, 0, bracketLen);       // bottom-left H
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                false, 0, 0, bracketLen);      // bottom-left V
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                true, 1, 0, bracketLen);       // bottom-right H
            CreateBracket(vfGo.transform, bracketColor, bracketThick, vfSide,
                false, 1, 0, bracketLen);      // bottom-right V

            // Shared font
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 22);

            // Title above viewfinder
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.text = Localization.Get("qr.title");
            titleTxt.fontSize = 22;
            titleTxt.color = Color.white;
            titleTxt.font = font;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.fontStyle = FontStyle.Bold;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.5f);
            titleRT.anchorMax = new Vector2(0.5f, 0.5f);
            titleRT.pivot = new Vector2(0.5f, 0.5f);
            titleRT.sizeDelta = new Vector2(Screen.width * 0.8f, 40);
            titleRT.anchoredPosition = new Vector2(0, vfSide / 2f + 50);

            // Status text below viewfinder
            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(transform, false);
            _statusText = statusGo.AddComponent<Text>();
            _statusText.text = Localization.Get("qr.instruction");
            _statusText.fontSize = 15;
            _statusText.color = new Color(0.8f, 0.8f, 0.8f);
            _statusText.font = font;
            _statusText.alignment = TextAnchor.MiddleCenter;
            var statusRT = statusGo.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0.5f, 0.5f);
            statusRT.anchorMax = new Vector2(0.5f, 0.5f);
            statusRT.pivot = new Vector2(0.5f, 0.5f);
            statusRT.sizeDelta = new Vector2(Screen.width * 0.8f, 30);
            statusRT.anchoredPosition = new Vector2(0, -vfSide / 2f - 40);

            // Close control inside the top-right safe area
            var cancelGo = new GameObject("CancelBtn");
            var controls = new GameObject("SafeControls", typeof(RectTransform));
            controls.transform.SetParent(transform, false);
            controls.AddComponent<SafeAreaFitter>();
            cancelGo.transform.SetParent(controls.transform, false);
            var cancelImg = cancelGo.AddComponent<Image>();
            cancelImg.color = new Color(1f, 1f, 1f, 0.15f);
            var cancelRT = cancelGo.GetComponent<RectTransform>();
            cancelRT.anchorMin = Vector2.one;
            cancelRT.anchorMax = Vector2.one;
            cancelRT.pivot = Vector2.one;
            cancelRT.sizeDelta = new Vector2(48, 48);
            cancelRT.anchoredPosition = new Vector2(-16, -16);
            var cancelBtn = cancelGo.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(Close);

            var cancelLblGo = new GameObject("Label");
            cancelLblGo.transform.SetParent(cancelGo.transform, false);
            var cancelLbl = cancelLblGo.AddComponent<Text>();
            cancelLbl.text = "×";
            cancelLbl.fontSize = 32;
            cancelLbl.color = Color.white;
            cancelLbl.font = font;
            cancelLbl.alignment = TextAnchor.MiddleCenter;
            var cancelLblRT = cancelLblGo.GetComponent<RectTransform>();
            cancelLblRT.anchorMin = Vector2.zero;
            cancelLblRT.anchorMax = Vector2.one;
            cancelLblRT.offsetMin = Vector2.zero;
            cancelLblRT.offsetMax = Vector2.zero;

            // Start camera
            StartCamera();
        }

        /// <summary>Creates one of the 4 dark mask rects around the viewfinder cutout.</summary>
        private void CreateMask(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var mrt = go.GetComponent<RectTransform>();
            mrt.anchorMin = anchorMin;
            mrt.anchorMax = anchorMax;
            mrt.offsetMin = offsetMin;
            mrt.offsetMax = offsetMax;
        }

        /// <summary>Creates a single corner bracket line (horizontal or vertical).</summary>
        private void CreateBracket(Transform parent, Color color, float thick, float side,
            bool horizontal, int cornerX, int cornerY, float lengthFrac)
        {
            var go = new GameObject("Bracket");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var brt = go.GetComponent<RectTransform>();

            float len = side * lengthFrac;
            float halfSide = side / 2f;
            float xSign = cornerX == 0 ? -1f : 1f;
            float ySign = cornerY == 0 ? -1f : 1f;

            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);

            if (horizontal)
            {
                brt.sizeDelta = new Vector2(len, thick);
                float x = xSign * (halfSide - len / 2f);
                float y = ySign * halfSide;
                brt.anchoredPosition = new Vector2(x, y);
            }
            else
            {
                brt.sizeDelta = new Vector2(thick, len);
                float x = xSign * halfSide;
                float y = ySign * (halfSide - len / 2f);
                brt.anchoredPosition = new Vector2(x, y);
            }
        }

        private void StartCamera()
        {
#if UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_WEBGL
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                _statusText.text = Localization.Get("qr.requesting");
                _waitingForPermission = true;
                _permissionRetryTime = Time.time + 0.5f;
                Application.RequestUserAuthorization(UserAuthorization.WebCam);
                return;
            }
#endif
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                _statusText.text = Localization.Get("qr.waiting");
                _waitingForPermission = true;
                _permissionRetryTime = Time.time + 0.5f;
                return;
            }

            _waitingForPermission = false;

            // Prefer back-facing camera
            string camName = devices[0].name;
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    camName = devices[i].name;
                    break;
                }
            }

            _webcam = new WebCamTexture(camName, 1280, 720, 30);
            _webcam.Play();
            _preview.texture = _webcam;
            _previewSized = false;
            _statusText.text = Localization.Get("qr.scanning");
        }

        private void Update()
        {
            if (_waitingForPermission && _webcam == null)
            {
                if (Time.time >= _permissionRetryTime)
                {
                    _permissionRetryTime = Time.time + 0.5f;
                    StartCamera();
                }
                return;
            }

            if (_found || _webcam == null || !_webcam.isPlaying) return;

            // Once the webcam has valid dimensions, size the preview to cover the screen
            if (!_previewSized && _webcam.width > 16)
            {
                _previewSized = true;
                FitPreviewToScreen();
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + _scanInterval;

            var normPixels = GetNormalizedPixels(out int normW, out int normH);
            if (_snapTex == null || _snapTex.width != normW || _snapTex.height != normH)
                _snapTex = new Texture2D(normW, normH, TextureFormat.RGBA32, false);

            _snapTex.SetPixels32(normPixels);
            _snapTex.Apply();

            string result = QRCodeReader.ReadFromTexture(_snapTex);
            if (!string.IsNullOrEmpty(result))
            {
                _found = true;
                _scannedResult = result;
                _statusText.text = Localization.Get("qr.found", result);
                _statusText.color = new Color(0.3f, 1f, 0.5f);
                Invoke(nameof(FireResult), 0.4f);
            }
        }

        /// <summary>
        /// Returns webcam pixels rotated/flipped so they match what the user sees on screen.
        /// videoRotationAngle 90/270 swaps width and height; videoVerticallyMirrored flips Y.
        /// </summary>
        private Color32[] GetNormalizedPixels(out int outW, out int outH)
        {
            int rawW = _webcam.width;
            int rawH = _webcam.height;
            var raw = _webcam.GetPixels32();
            int angle = _webcam.videoRotationAngle;
            bool mirrored = _webcam.videoVerticallyMirrored;

            // Flip vertically first when mirrored (before rotation)
            if (mirrored)
            {
                for (int y = 0; y < rawH / 2; y++)
                {
                    int y2 = rawH - 1 - y;
                    for (int x = 0; x < rawW; x++)
                    {
                        Color32 tmp = raw[y * rawW + x];
                        raw[y * rawW + x] = raw[y2 * rawW + x];
                        raw[y2 * rawW + x] = tmp;
                    }
                }
            }

            if (angle == 90) // 90° CW: new dimensions rawH x rawW
            {
                outW = rawH; outH = rawW;
                var rot = new Color32[rawW * rawH];
                for (int y = 0; y < rawH; y++)
                    for (int x = 0; x < rawW; x++)
                        rot[x * outW + (rawH - 1 - y)] = raw[y * rawW + x];
                return rot;
            }
            else if (angle == 180)
            {
                outW = rawW; outH = rawH;
                var rot = new Color32[rawW * rawH];
                for (int i = 0; i < raw.Length; i++)
                    rot[raw.Length - 1 - i] = raw[i];
                return rot;
            }
            else if (angle == 270) // 90° CCW
            {
                outW = rawH; outH = rawW;
                var rot = new Color32[rawW * rawH];
                for (int y = 0; y < rawH; y++)
                    for (int x = 0; x < rawW; x++)
                        rot[(rawW - 1 - x) * outW + y] = raw[y * rawW + x];
                return rot;
            }
            else // 0°: no rotation needed
            {
                outW = rawW; outH = rawH;
                return raw;
            }
        }

        /// <summary>
        /// Sizes and rotates the camera preview so it fills the screen (cover mode).
        /// Handles videoRotationAngle (90/270 on portrait phones) by rotating and
        /// scaling up so no black bars are visible.
        /// </summary>
        private void FitPreviewToScreen()
        {
            var prt = _preview.rectTransform;
            int angle = _webcam.videoRotationAngle;
            bool swapped = (angle == 90 || angle == 270);

            float camW = _webcam.width;
            float camH = _webcam.height;
            if (swapped) { float t = camW; camW = camH; camH = t; }

            float screenW = Screen.width;
            float screenH = Screen.height;

            // Scale so camera covers the full screen (no black bars)
            float scaleX = screenW / camW;
            float scaleY = screenH / camH;
            float scale = Mathf.Max(scaleX, scaleY);

            float finalW = camW * scale;
            float finalH = camH * scale;

            // Apply rotation, then set size to the un-rotated dimensions
            // (rotation swaps them visually)
            prt.localEulerAngles = new Vector3(0, 0, -angle);
            if (_webcam.videoVerticallyMirrored)
                prt.localScale = new Vector3(1, -1, 1);
            else
                prt.localScale = Vector3.one;

            if (swapped)
                prt.sizeDelta = new Vector2(finalH, finalW);
            else
                prt.sizeDelta = new Vector2(finalW, finalH);
        }

        private string _scannedResult;

        // Called by the native iOS plugin via UnitySendMessage. The argument is
        // the decoded string, or empty if the user cancelled.
        // ReSharper disable once UnusedMember.Local
        private void _OnNativeQRResult(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                OnCodeScanned?.Invoke(code);
            }
            // The native VC has already dismissed itself; just clean up.
            Destroy(gameObject);
        }

        private void FireResult()
        {
            OnCodeScanned?.Invoke(_scannedResult);
            Close();
        }

        private void Close()
        {
            if (_webcam != null && _webcam.isPlaying)
                _webcam.Stop();
            if (_snapTex != null)
                Destroy(_snapTex);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_webcam != null && _webcam.isPlaying)
                _webcam.Stop();
            if (_snapTex != null)
                Destroy(_snapTex);
#if UNITY_IOS && !UNITY_EDITOR
            // Make sure the native scanner is dismissed if our GameObject is
            // destroyed for any reason (scene change, etc.).
            try { _SandplayQR_Stop(); } catch { /* plugin not available */ }
#endif
        }
    }
}
