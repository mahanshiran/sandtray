using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;
using Sandplay.Objects;
using Sandplay.Sand;

namespace Sandplay.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Tool Buttons")]
        [SerializeField] private Button _selectBtn;
        [SerializeField] private Button _raiseBtn;
        [SerializeField] private Button _digBtn;
        [SerializeField] private Button _smoothBtn;
        [SerializeField] private Button _flattenBtn;
        [SerializeField] private Button _placeBtn;
        [SerializeField] private Button _moveBtn;
        [SerializeField] private Button _rotateBtn;

        [Header("Action Buttons")]
        [SerializeField] private Button _undoBtn;
        [SerializeField] private Button _redoBtn;
        [SerializeField] private Button _saveBtn;
        [SerializeField] private Button _loadBtn;
        [SerializeField] private Button _screenshotBtn;
        [SerializeField] private Button _analyzeBtn;
        [SerializeField] private Button _deleteObjBtn;
        [SerializeField] private Button _catalogToggleBtn;

        [Header("Brush Settings")]
        [SerializeField] private Slider _brushRadiusSlider;
        [SerializeField] private Slider _brushStrengthSlider;
        [SerializeField] private TMP_Text _brushRadiusLabel;
        [SerializeField] private TMP_Text _brushStrengthLabel;
        [SerializeField] private GameObject _brushSettingsPanel;

        [Header("Status")]
        [SerializeField] private TMP_Text _toolLabel;
        [SerializeField] private TMP_Text _statusLabel;

        [Header("Panels")]
        [SerializeField] private GameObject _catalogPanel;
        [SerializeField] private GameObject _analysisPanel;
        [SerializeField] private GameObject _saveLoadPanel;

        [Header("References")]
        [SerializeField] private SandToolController _sandToolController;
        [SerializeField] private ObjectPlacer _objectPlacer;

        private void Start()
        {
            // Tool buttons
            BindToolButton(_selectBtn, ToolMode.ObjectSelect);
            BindToolButton(_raiseBtn, ToolMode.SandRaise);
            BindToolButton(_digBtn, ToolMode.SandDig);
            BindToolButton(_smoothBtn, ToolMode.SandSmooth);
            BindToolButton(_flattenBtn, ToolMode.SandFlatten);
            BindToolButton(_moveBtn, ToolMode.ObjectMove);
            BindToolButton(_rotateBtn, ToolMode.ObjectRotate);

            // Action buttons
            if (_undoBtn) _undoBtn.onClick.AddListener(() => Data.UndoManager.Instance?.UndoLast());
            if (_redoBtn) _redoBtn.onClick.AddListener(() => Data.UndoManager.Instance?.RedoLast());
            if (_saveBtn) _saveBtn.onClick.AddListener(OnSaveClicked);
            if (_loadBtn) _loadBtn.onClick.AddListener(OnLoadClicked);
            if (_screenshotBtn) _screenshotBtn.onClick.AddListener(OnScreenshotClicked);
            if (_analyzeBtn) _analyzeBtn.onClick.AddListener(OnAnalyzeClicked);
            if (_deleteObjBtn) _deleteObjBtn.onClick.AddListener(() => _objectPlacer?.RemoveSelected());
            if (_catalogToggleBtn) _catalogToggleBtn.onClick.AddListener(ToggleCatalog);

            // Brush sliders
            if (_brushRadiusSlider && _sandToolController)
            {
                var config = GameManager.Instance.Config;
                _brushRadiusSlider.minValue = config.MinBrushRadius;
                _brushRadiusSlider.maxValue = config.MaxBrushRadius;
                _brushRadiusSlider.value = _sandToolController.BrushRadius;
                _brushRadiusSlider.onValueChanged.AddListener(v =>
                {
                    _sandToolController.BrushRadius = v;
                    if (_brushRadiusLabel) _brushRadiusLabel.text = Localization.Get("brush.radius_val", v);
                });
            }

            if (_brushStrengthSlider && _sandToolController)
            {
                _brushStrengthSlider.minValue = 0.1f;
                _brushStrengthSlider.maxValue = 1f;
                _brushStrengthSlider.value = _sandToolController.BrushStrength;
                _brushStrengthSlider.onValueChanged.AddListener(v =>
                {
                    _sandToolController.BrushStrength = v;
                    if (_brushStrengthLabel) _brushStrengthLabel.text = Localization.Get("brush.strength_val", v);
                });
            }

            EventBus.Subscribe<ToolModeChangedEvent>(OnToolModeChanged);
            EventBus.Subscribe<ObjectSelectedEvent>(OnObjectSelected);

            UpdateToolLabel(GameManager.Instance.CurrentTool);
        }

        private void BindToolButton(Button btn, ToolMode mode)
        {
            if (btn != null)
                btn.onClick.AddListener(() => GameManager.Instance.SetToolMode(mode));
        }

        private void OnToolModeChanged(ToolModeChangedEvent evt)
        {
            UpdateToolLabel(evt.NewMode);

            // Show brush settings only for sand tools
            bool isSandTool = evt.NewMode == ToolMode.SandRaise || evt.NewMode == ToolMode.SandDig ||
                              evt.NewMode == ToolMode.SandSmooth || evt.NewMode == ToolMode.SandFlatten;
            if (_brushSettingsPanel) _brushSettingsPanel.SetActive(isSandTool);

            // Show delete button only in select mode
            if (_deleteObjBtn) _deleteObjBtn.gameObject.SetActive(evt.NewMode == ToolMode.ObjectSelect);
        }

        private void OnObjectSelected(ObjectSelectedEvent evt)
        {
            if (_deleteObjBtn)
                _deleteObjBtn.interactable = evt.PlacedObject != null;
            if (_statusLabel)
                _statusLabel.text = evt.PlacedObject != null
                    ? Localization.Get("status.selected", evt.PlacedObject.ObjectData?.DisplayName ?? "Object")
                    : "";
        }

        private void UpdateToolLabel(ToolMode mode)
        {
            if (_toolLabel == null) return;
            _toolLabel.text = mode switch
            {
                ToolMode.ObjectSelect => Localization.Get("hud.select"),
                ToolMode.SandRaise => Localization.Get("hud.raise_sand"),
                ToolMode.SandDig => Localization.Get("hud.dig_sand"),
                ToolMode.SandSmooth => Localization.Get("hud.smooth_sand"),
                ToolMode.SandFlatten => Localization.Get("hud.flatten_sand"),
                ToolMode.ObjectPlace => Localization.Get("hud.place_object"),
                ToolMode.ObjectMove => Localization.Get("hud.move_object"),
                ToolMode.ObjectRotate => Localization.Get("hud.rotate_object"),
                ToolMode.ObjectScale => Localization.Get("hud.scale_object"),
                _ => Localization.Get("hud.none")
            };
        }

        private void OnSaveClicked()
        {
            if (_saveLoadPanel) _saveLoadPanel.SetActive(true);
            // Default quick save
            Data.SessionManager.Instance?.SaveSession("QuickSave");
            SetStatus(Localization.Get("hud.saved"));
        }

        private void OnLoadClicked()
        {
            if (_saveLoadPanel) _saveLoadPanel.SetActive(true);
            // Default quick load
            Data.SessionManager.Instance?.LoadSession("QuickSave");
            SetStatus(Localization.Get("hud.loaded"));
        }

        private void OnScreenshotClicked()
        {
            string path = Data.ScreenshotManager.Instance?.SaveScreenshot();
            SetStatus(Localization.Get("hud.screenshot"));
        }

        private void OnAnalyzeClicked()
        {
            if (_analysisPanel) _analysisPanel.SetActive(true);
            EventBus.Publish(new AnalysisRequestedEvent());
            SetStatus(Localization.Get("hud.analysis"));
        }

        private void ToggleCatalog()
        {
            if (_catalogPanel)
                _catalogPanel.SetActive(!_catalogPanel.activeSelf);
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel) _statusLabel.text = msg;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ToolModeChangedEvent>(OnToolModeChanged);
            EventBus.Unsubscribe<ObjectSelectedEvent>(OnObjectSelected);
        }
    }
}
