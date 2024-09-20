using InhabitantChess.BoardGame;
using InhabitantChess.Util;
using UnityEngine;

namespace InhabitantChess
{
    public enum ChessPlayerState
    {
        None,
        Seated,
        StandingUp,
        EnteringOverhead,
        InOverhead,
        ExitingOverhead
    }

    public class ChessGame : MonoBehaviour
    {
        public static ChessPlayerState PlayerState { get; private set; }
        public AudioEffects AudioEffects { get; private set; }

        public delegate void PlayerInteractionEvent(ChessGame chess);
        public PlayerInteractionEvent OnSitDown;
        public PlayerInteractionEvent OnStoodUp;

        public delegate void ChessPlayerAudioEvent();
        public ChessPlayerAudioEvent OnLeanForward;
        public ChessPlayerAudioEvent OnLeanBackward;

        private const float MaxLeanAmount = 1f, LeanSpeed = 1.5f, LeanSoundCooldown = 1f;

        private PlayerCameraController _playerCamController;
        private OverheadCameraController _overheadCamController;
        private BoardGameController _bgController;
        private BoardController _bController;
        private PlayerAttachPoint _attachPoint;
        private InteractZone _seatInteract;
        private float _exitSeatTime, _initOverheadTime, _exitOverheadTime;
        private float _oldLeanAmt, _leanAmt, _lastLeanSoundTime;

        private InhabitantChess InhabitantChess => InhabitantChess.Instance;
        private ScreenPromptController ScreenPrompts => ScreenPromptController.Instance;

        private void Start()
        {
            enabled = false;

            InhabitantChess.ICPrefabs prefabs = InhabitantChess.Prefabs;
            _bController = transform.Find("BoardGame_Board").gameObject.AddComponent<BoardController>();
            _bController.Init(prefabs.space, prefabs.blocker, prefabs.antler, prefabs.eye, InhabitantChess.HighlightMaterials);
            _bgController = gameObject.AddComponent<BoardGameController>();
            AudioEffects = gameObject.AddComponent<AudioEffects>();

            CreateGameSeat();
            CreateOverheadCamera();

            _bgController.OnStartGame += OnStartGame;
            _bgController.OnStopGame += OnStopGame;
            _seatInteract.OnPressInteract += OnPressInteract;
            _seatInteract.OnPressInteract += _bgController.OnPressInteract;
        }

        private void OnDestroy()
        {
            if (InhabitantChess.CurrentGame == this)
                PlayerState = ChessPlayerState.None;
            _seatInteract.OnPressInteract -= OnPressInteract;
            _seatInteract.OnPressInteract -= _bgController.OnPressInteract;
        }

        private void Update()
        {
            // leaning, exiting chair while seated
            if (PlayerState == ChessPlayerState.Seated)
            {
                if (OWInput.IsNewlyPressed(Controls.ExitOverhead, InputMode.All))
                {
                    //_bgController.ExitGame();
                    BeginStandingUp();
                }
                else if (OWInput.IsPressed(Controls.PanCamera, InputMode.All))
                {
                    float v = OWInput.GetAxisValue(Controls.PanCamera).y;
                    _oldLeanAmt = _leanAmt;
                    _leanAmt += v * LeanSpeed * Time.deltaTime;
                    _leanAmt = Mathf.Clamp(_leanAmt, 0.0f, MaxLeanAmount);
                    UpdateLeanSFX();
                }
            }

            // transition in/out of overhead, or update transition in progress
            if (PlayerState != ChessPlayerState.EnteringOverhead)
            {
                if (PlayerState == ChessPlayerState.Seated && OWInput.IsNewlyPressed(Controls.Overhead, InputMode.All))
                {
                    EnterOverheadView();
                }
                else if (PlayerState == ChessPlayerState.InOverhead && (OWInput.IsNewlyPressed(Controls.Overhead, InputMode.All) ||
                    OWInput.IsNewlyPressed(Controls.ExitOverhead, InputMode.All)))
                {
                    Controls.ExitOverhead.ConsumeInput();
                    ExitOverheadView();
                }
            }
            else
            {
                UpdateEnterOverheadTransition();
            }
        }

        private void UpdateLeanSFX()
        {
            float leanThreshold = MaxLeanAmount / 3;
            bool playedSound = false;

            if (Time.time > _lastLeanSoundTime + LeanSoundCooldown)
            {
                // leaning forward
                if (_oldLeanAmt <= leanThreshold && leanThreshold < _leanAmt)
                {
                    playedSound = true;
                    OnLeanForward?.Invoke();
                }
                // leaning backward
                else if (_leanAmt <= leanThreshold && leanThreshold < _oldLeanAmt)
                {
                    playedSound = true;
                    OnLeanBackward?.Invoke();
                }
            }
            if (playedSound) _lastLeanSoundTime = Time.time;
        }

        private void UpdateEnterOverheadTransition()
        {
            if (Time.time > _initOverheadTime + 0.45f)
            {
                PlayerState = ChessPlayerState.InOverhead;
                InhabitantChess.CameraAPI.EnterCamera(_overheadCamController.OverheadCam);
                _overheadCamController.ResetPosition();
            }
        }

        private void FixedUpdate()
        {
            // delay copied from ship cockpit controller to force recentering of camera
            if (PlayerState == ChessPlayerState.StandingUp && Time.time >= _exitSeatTime + 0.2f)
            {
                CompleteStandingUp();
            }
            if (PlayerState == ChessPlayerState.ExitingOverhead && Time.time >= _exitOverheadTime + 0.45f)
            {
                PlayerState = ChessPlayerState.Seated;
            }
        }

        public float GetLean()
        {
            return _leanAmt;
        }

        public (BoardController.ChessPiece, (int, int))? GetPlayerFocusedSpace()
        {
            BoardController.ChessPiece? currentPlayer = _bgController.GetCurrentPlayer();
            SpaceController focusedSpace = _bgController.GetPlayerFocusedSpace();

            if (currentPlayer != null && focusedSpace != null)
                return (currentPlayer.Value, focusedSpace.Position);
            return null;
        }

        public void ForceStandUp()
        {
            if (PlayerState != ChessPlayerState.Seated) return;
            CompleteStandingUp();
        }

        public void EnableSeatInteract()
        {
            if (_seatInteract == null) return;
            _seatInteract.ResetInteraction();
            _seatInteract.EnableInteraction();
        }

        public void DisableSeatInteract()
        {
            if (_seatInteract == null) return;
            _seatInteract.DisableInteraction();
        }

        private void CreateGameSeat()
        {
            GameObject gameSeat = Instantiate(InhabitantChess.CockpitClone, transform);
            gameSeat.transform.localPosition = Vector3.right;
            gameSeat.transform.localRotation = Quaternion.Euler(0, 270, 0);
            gameSeat.SetActive(true);
            _attachPoint = gameSeat.GetComponent<PlayerAttachPoint>();
            _seatInteract = gameSeat.GetComponent<InteractZone>();
        }

        private void CreateOverheadCamera()
        {
            _bgController.PlayerManip = Locator.GetPlayerTransform().GetComponentInChildren<FirstPersonManipulator>();
            _playerCamController = Locator.GetPlayerCameraController();
            (OWCamera owCam, _) = InhabitantChess.CameraAPI.CreateCustomCamera("Overhead Camera");
            owCam.transform.SetParent(transform);
            owCam.transform.localPosition = new Vector3(0f, 2f, 0);
            owCam.transform.localRotation = Quaternion.Euler(90, 270, 0);
            _overheadCamController = owCam.gameObject.AddComponent<OverheadCameraController>();
            _overheadCamController.Setup();
        }

        private void RefreshScorePrompt()
        {
            (int won, int lost) = _bgController.GetScore();
            ScreenPrompts.SetScore(won, lost);
        }

        private void EnterOverheadView()
        {
            // my ability to directly lift mobius' code grows stronger with every passing day
            PlayerState = ChessPlayerState.EnteringOverhead;
            _initOverheadTime = Time.time;
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.BoardMove, false);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Lean, false);

            _playerCamController.SnapToDegreesOverSeconds(0f, -48.5f, 0.5f, true);
            _playerCamController.SnapToFieldOfView(24f, 0.5f, true);
            OWInput.ChangeInputMode(InputMode.Map);
        }

        private void ExitOverheadView()
        {
            PlayerState = ChessPlayerState.ExitingOverhead;
            _exitOverheadTime = Time.time;
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.BoardMove, true);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Lean, true);

            InhabitantChess.CameraAPI.ExitCamera(_overheadCamController.OverheadCam);
            _overheadCamController.ResetPosition();
            _playerCamController.CenterCameraOverSeconds(0.5f, true);
            _playerCamController.SnapToInitFieldOfView(0.5f, true);
            OWInput.ChangeInputMode(InputMode.Character);
        }

        private void BeginStandingUp()
        {
            _leanAmt = 0f;
            _oldLeanAmt = 0f;
            _playerCamController.CenterCameraOverSeconds(0.2f, false);
            _exitSeatTime = Time.time;
            PlayerState = ChessPlayerState.StandingUp;
        }

        private void CompleteStandingUp()
        {
            enabled = false;
            _bgController.enabled = false;
            _attachPoint.DetachPlayer();
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Score, false);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.BoardMove, false);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Overhead, false);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Lean, false);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.SpawnChessGame, InhabitantChess.Debugging);
            PlayerState = ChessPlayerState.None;
            OnStoodUp?.Invoke(this);
        }

        private void OnPressInteract()
        {
            enabled = true;
            _bgController.enabled = true;
            _attachPoint.AttachPlayer();
            RefreshScorePrompt();
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Score, true);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.BoardMove, true);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Overhead, true);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.Lean, true);
            ScreenPrompts.SetPromptVisibility(ScreenPromptController.PromptType.SpawnChessGame, false);
            PlayerState = ChessPlayerState.Seated;
            OnSitDown?.Invoke(this);
        }

        private void OnStartGame()
        {
            _seatInteract.ChangePrompt((UITextType)Translations.GetUITextType("IC_INTERACT"));
        }

        private void OnStopGame()
        {
            RefreshScorePrompt();
            _seatInteract.ChangePrompt((UITextType)Translations.GetUITextType("IC_PLAYAGAIN"));
        }
    }
}
