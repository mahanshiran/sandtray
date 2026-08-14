using UnityEngine;

namespace Sandplay.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameConfig _config;

        public GameConfig Config => _config;

        private ToolMode _currentTool = ToolMode.ObjectSelect;
        public ToolMode CurrentTool => _currentTool;

        private PlayerRole _networkRole = PlayerRole.Patient;
        public PlayerRole NetworkRole
        {
            get => _networkRole;
            set
            {
                _networkRole = value;
                EventBus.Publish(new NetworkRoleAssignedEvent { Role = value });
            }
        }
        public bool IsSpectator => _networkRole != PlayerRole.Patient;
        public bool IsPsychologist => _networkRole == PlayerRole.Psychologist;
        public bool IsObserver => _networkRole == PlayerRole.Observer;
        public bool IsPatient => _networkRole == PlayerRole.Patient;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(GameConfig config)
        {
            _config = config;
        }

        public void SetToolMode(ToolMode mode)
        {
            var prev = _currentTool;
            _currentTool = mode;
            EventBus.Publish(new ToolModeChangedEvent { NewMode = mode, PreviousMode = prev });
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
