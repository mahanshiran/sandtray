using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Objects;

namespace Sandplay.Core
{
    // ─────────────────────────────────────────────────────────────────────────
    // Partial: "Patient is acting" indicator for Therapist mode.
    //
    // When the therapist (host) — or any non-patient peer, including the
    // patient itself for self-feedback — receives a PointerHover event, we show:
    //   • a small unlit sphere at the patient's last cursor world position,
    //     colored by what the patient is doing (cyan = sculpt, amber = paint,
    //     magenta = place); object placements briefly enlarge the dot as a pulse
    //   • a top-center badge whose text matches the kind ("Patient is sculpting…",
    //     "…painting…", "…placing an object")
    //
    // The dot fades to invisible after ~1.5 s without further updates and hard-
    // hides at ~2.5 s, so the screen stays calm when the patient is idle.
    //
    // Hooked into the bootstrap flow via EnsurePatientPointerOverlay(), which
    // is called from CreateUI just after the Agora panel is built.
    // ─────────────────────────────────────────────────────────────────────────
    public partial class SceneBootstrapper
    {
        private GameObject _patientPointerDot;         // 3D quad/sphere in world
        private GameObject _patientActingBadge;        // top-center badge
        private Image _patientActingBadgeBg;
        private TextMeshProUGUI _patientActingBadgeTxt;
        private string _patientActingBadgeLocKey = "therapist.patient_acting";

        private float _lastPatientPointerAt = -999f;
        private float _placePulseUntil = -999f;
        private PatientPointerKind _lastPatientPointerKind = PatientPointerKind.Sculpt;
        private bool _patientPointerOverlayWired;

        private const float PatientPointerFadeAfterSec = 1.5f;
        private const float PatientPointerHardHideSec = 2.5f;
        private const float PlacePulseDurationSec = 0.45f;
        private static readonly Vector3 PointerDotBaseScale = new Vector3(0.06f, 0.06f, 0.06f);
        private const float PlacePulseMaxScaleMult = 2.6f;

        // Color palette per kind. Picked for high contrast against beige sand
        // *and* dark walls; matches the badge background so the eye links them.
        private static readonly Color SculptColor = new Color(0.30f, 0.85f, 1.00f, 1f); // cyan
        private static readonly Color PaintColor = new Color(1.00f, 0.78f, 0.30f, 1f); // amber
        private static readonly Color PlaceColor = new Color(1.00f, 0.40f, 0.85f, 1f); // magenta
        private static readonly Color IdleColor = new Color(0.70f, 0.70f, 0.74f, 1f); // gray

        private void EnsurePatientPointerOverlay()
        {
            if (_patientPointerOverlayWired) return;
            _patientPointerOverlayWired = true;

            // ── Build the world-space dot (small unlit sphere) ────────────
            _patientPointerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _patientPointerDot.name = "PatientPointerDot";
            // Strip the default collider — purely cosmetic, must not interfere
            // with sand raycasts or object selection.
            var col = _patientPointerDot.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _patientPointerDot.transform.localScale = PointerDotBaseScale;
            var rend = _patientPointerDot.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                // Use a per-instance material so we can mutate color without
                // affecting the shared sphere material in the asset database.
                var mat = new Material(sh) { color = SculptColor };
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
            if (_sandboxRoot != null)
                _patientPointerDot.transform.SetParent(_sandboxRoot.transform, true);
            _patientPointerDot.SetActive(false);

            // ── Build the top-center "Patient is acting" badge ────────────
            if (_safeArea != null)
            {
                _patientActingBadge = new GameObject("PatientActingBadge");
                _patientActingBadge.transform.SetParent(_safeArea.transform, false);
                _patientActingBadgeBg = _patientActingBadge.AddComponent<Image>();
                _patientActingBadgeBg.color = SculptColor;
                ApplyRoundedCorners(_patientActingBadgeBg);
                var brt = _patientActingBadge.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
                brt.anchoredPosition = new Vector2(0f, -12f);
                brt.sizeDelta = new Vector2(240f, 30f);

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(_patientActingBadge.transform, false);
                _patientActingBadgeTxt = txtGo.AddComponent<TextMeshProUGUI>();
                _patientActingBadgeTxt.text = Localization.Get(_patientActingBadgeLocKey);
                _patientActingBadgeTxt.font = GetUIFont();
                _patientActingBadgeTxt.fontSize = 14;
                _patientActingBadgeTxt.fontStyle = FontStyles.Bold;
                _patientActingBadgeTxt.color = new Color(0.05f, 0.15f, 0.22f, 1f);
                _patientActingBadgeTxt.alignment = TextAlignmentOptions.Center;
                var trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10f, 0f); trt.offsetMax = new Vector2(-10f, 0f);
                // Note: we deliberately do NOT register this with TrackLocalized.
                // The badge text is volatile (changes per pointer kind, see
                // UpdateBadgeForKind) so registering would either leak entries
                // or fight us on language change. Pointer events arrive within
                // ~1.5s of activity, so a stale label after language switch is
                // self-correcting.
                _patientActingBadge.SetActive(false);
            }

            // ── Subscribe to network events ───────────────────────────────
            if (NetworkBootstrapper.Instance != null)
            {
                NetworkBootstrapper.Instance.OnPatientPointerHover += HandlePatientPointerHover;
            }
            // Patient-side: emit a Place pulse whenever the patient drops an
            // object into the scene. The send is gated to actual patient peers
            // by SendClientPointerHover/CanSendClientAction, so this is safe to
            // subscribe unconditionally on every peer.
            EventBus.Subscribe<ObjectPlacedEvent>(OnPatientObjectPlaced);

            // ── Drive fade-out via a coroutine (no Update hook needed) ────
            StartCoroutine(PatientPointerFadeLoop());
        }

        private void OnPatientObjectPlaced(ObjectPlacedEvent evt)
        {
            // Only the patient broadcasts. Other peers receive via the relay.
            if (GameManager.Instance == null || !GameManager.Instance.IsPatient) return;
            if (NetworkBootstrapper.Instance == null) return;
            if (evt.PlacedObject == null) return;
            NetworkBootstrapper.Instance.SendClientPointerHover(
                evt.PlacedObject.transform.position, PatientPointerKind.Place);
        }

        private void HandlePatientPointerHover(Vector3 worldPos, PatientPointerKind kind)
        {
            _lastPatientPointerAt = Time.unscaledTime;
            _lastPatientPointerKind = kind;
            if (kind == PatientPointerKind.Place)
                _placePulseUntil = Time.unscaledTime + PlacePulseDurationSec;

            if (_patientPointerDot != null)
            {
                _patientPointerDot.transform.position = worldPos + new Vector3(0f, 0.02f, 0f);
                if (!_patientPointerDot.activeSelf) _patientPointerDot.SetActive(true);
                var r = _patientPointerDot.GetComponent<MeshRenderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    var c = ColorForKind(kind); c.a = 1f;
                    r.sharedMaterial.color = c;
                }
            }
            UpdateBadgeForKind(kind);
            if (_patientActingBadge != null && !_patientActingBadge.activeSelf)
                _patientActingBadge.SetActive(true);
        }

        private void UpdateBadgeForKind(PatientPointerKind kind)
        {
            if (_patientActingBadgeTxt == null) return;
            string key = kind switch
            {
                PatientPointerKind.Paint => "therapist.patient_painting",
                PatientPointerKind.Place => "therapist.patient_placing",
                _ => "therapist.patient_acting", // sculpt / idle fallback
            };
            _patientActingBadgeLocKey = key;
            _patientActingBadgeTxt.text = Localization.Get(key);
            if (_patientActingBadgeBg != null)
            {
                var c = ColorForKind(kind); c.a = 0.92f;
                _patientActingBadgeBg.color = c;
            }
        }

        private static Color ColorForKind(PatientPointerKind kind) => kind switch
        {
            PatientPointerKind.Sculpt => SculptColor,
            PatientPointerKind.Paint => PaintColor,
            PatientPointerKind.Place => PlaceColor,
            _ => IdleColor,
        };

        private IEnumerator PatientPointerFadeLoop()
        {
            // Tighter tick than fade window so the place-pulse animation reads
            // smoothly. 30 Hz is plenty for a single sphere transform.
            var wait = new WaitForSecondsRealtime(1f / 30f);
            while (true)
            {
                yield return wait;
                if (_patientPointerDot == null) continue;

                // ── Place pulse: scale up & relax back. Independent of fade. ──
                float now = Time.unscaledTime;
                float pulseT = (_placePulseUntil - now) / PlacePulseDurationSec;
                float scaleMult = 1f;
                if (pulseT > 0f)
                {
                    // Ease-out: peak at start of window, decay to 1 at end.
                    float k = Mathf.Clamp01(pulseT);
                    scaleMult = 1f + (PlacePulseMaxScaleMult - 1f) * k * k;
                }
                _patientPointerDot.transform.localScale = PointerDotBaseScale * scaleMult;

                if (_lastPatientPointerAt < 0f) continue;
                float since = now - _lastPatientPointerAt;
                if (since > PatientPointerHardHideSec)
                {
                    if (_patientPointerDot.activeSelf) _patientPointerDot.SetActive(false);
                    if (_patientActingBadge != null && _patientActingBadge.activeSelf)
                        _patientActingBadge.SetActive(false);
                }
                else if (since > PatientPointerFadeAfterSec)
                {
                    var r = _patientPointerDot.GetComponent<MeshRenderer>();
                    if (r != null && r.sharedMaterial != null)
                    {
                        float t = 1f - Mathf.Clamp01((since - PatientPointerFadeAfterSec)
                            / (PatientPointerHardHideSec - PatientPointerFadeAfterSec));
                        var c = r.sharedMaterial.color; c.a = t;
                        r.sharedMaterial.color = c;
                    }
                }
            }
        }
    }
}
