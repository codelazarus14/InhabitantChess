using HarmonyLib;
using InhabitantChess.BoardGame;
using InhabitantChess.Util;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

// TODO: eyes of the past branch
// - game instancing system: move some of the per-game logic (instantiation, player state, audio, update loops) to separate class
//   to support creating separate game instances and npc encounters in the world
// - same w prisoner sequence - make some generic class for wrapping dialogue/scripted sequences around setting up and clearing away the game
// - remove shortcut?
// - interacting with the board - prompt user to interact w spaces (screenprompt, highlight glows brighter)
// - display rules (ask the inhabitant diff options thru dialogue, optional "can i review the rules")
// - difficulty options (per game/instance), parameter for decision making, dialogue option

namespace InhabitantChess
{
    public class InhabitantChess : ModBehaviour
    {
        public static InhabitantChess Instance { get; private set; }
        public GameObject BoardGame { get; private set; }
        public GameObject PrisonCell { get; private set; }
        public PrisonerSequence PrisonerSequence { get; private set; }
        public AudioEffects AudioEffects { get; private set; }
        public Shortcut Shortcut { get; private set; }
        public ChessPlayerState PlayerState { get; private set; }
        public (bool moves, bool pieces, bool beam) HighlightSettings { get; private set; }
        public bool ShortcutEnabled { get; private set; }

        public delegate void ChessPlayerAudioEvent();
        public ChessPlayerAudioEvent OnLeanForward;
        public ChessPlayerAudioEvent OnLeanBackward;
        public ChessPlayerAudioEvent OnSitDown;
        public ChessPlayerAudioEvent OnStandUp;

        private delegate void ConfigureEvent();
        private ConfigureEvent OnConfigure;

        private class ICData
        {
            public bool unlockedShortcut;
        }
        private ICData _saveData;

        private struct ICPrefabs
        {
            public GameObject chess;
            public GameObject space;
            public GameObject blocker;
            public GameObject antler;
            public GameObject eye;
        }
        private ICPrefabs _prefabs;

        private const string SaveFileName = "ic_save.json";
        private static Shader s_standardShader = Shader.Find("Standard");

        private ICommonCameraAPI _cameraAPI;
        private BoardGameController _bgController;
        private PlayerCameraController _playerCamController;
        private OverheadCameraController _overheadCamController;
        private ScreenPrompts _screenPrompts;
        private GameObject _cockpitClone;
        private PlayerAttachPoint _attachPoint;
        private InteractZone _seatInteract;
        private Shader _highlightShader;
        private Material[] _highlightMaterials;
        private float _exitSeatTime, _initOverheadTime, _exitOverheadTime;
        private float _oldLeanAmt, _leanAmt, _lastLeanSoundTime, _maxLeanAmt = 1f, _leanSpeed = 1.5f, _leanSoundCooldown = 1f;
        private bool _hasCachedData;

        private void Awake()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        private void Start()
        {
            if (EntitlementsManager.IsDlcOwned() != EntitlementsManager.AsyncOwnershipStatus.Owned)
            {
                Logger.LogError("EOTE not detected - disabling InhabitantChess :(");
                enabled = false;
                return;
            }
            _cameraAPI = ModHelper.Interaction.TryGetModApi<ICommonCameraAPI>("xen.CommonCameraUtility");
            var dependants = GetDependants();
            bool instancing = dependants.Count > 0;
            if (instancing)
                Logger.Log($"Dependencies detected - enabling chess game instancing");

            // TODO testing - delete later
            //instancing = true;

            AssetBundle bundle = ModHelper.Assets.LoadBundle("Assets/triboard");
            _prefabs = LoadPrefabs(bundle, "assets/prefabs/triboard/");
            TextAsset prisonerDialogue = LoadText("Assets/PrisonerDialogue.xml");
            Translations.LoadTranslations();

            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene == OWScene.SolarSystem && !_hasCachedData)
                    CacheExistingData();
                else if (!_hasCachedData)
                {
                    Logger.LogError($"Solar System scene hasn't been loaded - data is missing!");
                    return;
                }

                PlayerState = ChessPlayerState.None;

                if (instancing) return;

                PrisonCell = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_Underground/Sector_PrisonCell");

                BoardGame = Instantiate(_prefabs.chess, PrisonCell.transform);
                BoardGame.transform.localPosition = new Vector3(4, -35.105f, 0.2f);
                BoardGame.transform.localRotation = Quaternion.Euler(0, 270, 0);

                Synchronizer synch = BoardGame.AddComponent<Synchronizer>();
                BoardController bController = BoardGame.transform.Find("BoardGame_Board").gameObject.AddComponent<BoardController>();
                bController.SpacePrefab = _prefabs.space;
                bController.BlockerPrefab = _prefabs.blocker;
                bController.AntlerPrefab = _prefabs.antler;
                bController.EyePrefab = _prefabs.eye;
                bController.Synchronizer = synch;
                bController.HighlightShader = _highlightShader;
                bController.HighlightMaterials = _highlightMaterials;
                _bgController = BoardGame.AddComponent<BoardGameController>();

                StartCoroutine(CreateGameSeat(BoardGame.transform, Vector3.right, Quaternion.Euler(0, 270, 0)));

                _screenPrompts = BoardGame.AddComponent<ScreenPrompts>();
                PrisonerSequence = PrisonCell.AddComponent<PrisonerSequence>();
                PrisonerSequence.SetText(prisonerDialogue);
                Shortcut = PrisonCell.AddComponent<Shortcut>();
                AudioEffects = PrisonCell.AddComponent<AudioEffects>();

                // set up camera w util later
                GlobalMessenger.AddListener("EnterDreamWorld", new Callback(OnEnterDreamworld));
                TextTranslation.Get().OnLanguageChanged += Translations.OnLanguageChanged;
                _seatInteract.OnPressInteract += OnPressInteract;
                _seatInteract.OnPressInteract += _bgController.OnPressInteract;
                OnConfigure += () => _bgController.OnHighlightConfigure(HighlightSettings);
                OnConfigure += () => Shortcut.EnableShortcut(ShortcutEnabled);

                Logger.LogSuccess("Finished setup");
            };
        }

        private void OnDestroy()
        {
            GlobalMessenger.RemoveListener("EnterDreamWorld", new Callback(OnEnterDreamworld));
            TextTranslation.Get().OnLanguageChanged -= Translations.OnLanguageChanged;
            if (_seatInteract != null)
            {
                _seatInteract.OnPressInteract -= OnPressInteract;
                _seatInteract.OnPressInteract -= _bgController.OnPressInteract;
            }
            OnConfigure -= () => Shortcut.EnableShortcut(ShortcutEnabled);
            OnConfigure -= () => _bgController.OnHighlightConfigure(HighlightSettings);
        }

        private void Update()
        {
            if (_seatInteract == null || PlayerState == ChessPlayerState.None) return;

            if (PlayerState == ChessPlayerState.Seated)
            {
                if (OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
                {
                    //_bgController.ExitGame();
                    StandUp();
                }
                else if (OWInput.IsPressed(InputLibrary.moveXZ, InputMode.All))
                {
                    float v = OWInput.GetAxisValue(InputLibrary.moveXZ).y;
                    _oldLeanAmt = _leanAmt;
                    _leanAmt += v * _leanSpeed * Time.deltaTime;
                    _leanAmt = Mathf.Clamp(_leanAmt, 0.0f, _maxLeanAmt);
                    CheckAndFireLeanSFX();
                }
                if (!_bgController.Playing)
                {
                    _seatInteract.ChangePrompt((UITextType)Translations.GetUITextType("IC_PLAYAGAIN"));

                    (int won, int lost) = _bgController.GetScore();
                    _screenPrompts.SetScore(won, lost);
                }
            }
            if (PlayerState != ChessPlayerState.EnteringOverhead)
            {
                if (PlayerState == ChessPlayerState.Seated && OWInput.IsNewlyPressed(InputLibrary.landingCamera, InputMode.All))
                {
                    EnterOverheadView();
                }
                else if (PlayerState == ChessPlayerState.InOverhead && (OWInput.IsNewlyPressed(InputLibrary.landingCamera, InputMode.All) ||
                        OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All)))
                {
                    InputLibrary.cancel.ConsumeInput();
                    ExitOverheadView();
                }
            }
            else
            {
                UpdateEnterOverheadTransition();
            }
        }

        private void UpdateEnterOverheadTransition()
        {
            if (Time.time > _initOverheadTime + 0.45f)
            {
                PlayerState = ChessPlayerState.InOverhead;
                _cameraAPI.EnterCamera(_overheadCamController.OverheadCam);
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


        public override void Configure(IModConfig config)
        {
            _saveData ??= ModHelper.Storage.Load<ICData>(SaveFileName) ?? new();
            //Logger.Log($"Shortcut unlocked? {_saveData.unlockedShortcut}");

            ShortcutEnabled = /*_saveData.unlockedShortcut &&*/ config.GetSettingsValue<bool>("Enable Shortcut");
            HighlightSettings = new(config.GetSettingsValue<bool>("Moves Highlighting"),
                                config.GetSettingsValue<bool>("Piece Highlighting"),
                                config.GetSettingsValue<bool>("Beam Highlighting"));
            OnConfigure?.Invoke();
        }

        public void ShortcutUnlocked()
        {
            //Logger.Log("Player unlocked shortcut!");
            if (!_saveData.unlockedShortcut) _saveData.unlockedShortcut = true;
            ModHelper.Storage.Save(_saveData, SaveFileName);

            ShortcutEnabled = _saveData.unlockedShortcut && ModHelper.Config.GetSettingsValue<bool>("Enable Shortcut");
        }

        public void StandUp()
        {
            _leanAmt = 0f;
            _oldLeanAmt = 0f;
            _playerCamController.CenterCameraOverSeconds(0.2f, false);
            PlayerState = ChessPlayerState.StandingUp;
            _exitSeatTime = Time.time;
        }

        private void CacheExistingData()
        {
            // TODO: see if we can replace this with Resources.loading stuff, creating objects on the fly
            GameObject sampleBoardGame = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_1/Simulation_DreamZone_1/Props_DreamZone_1/Props_GenericHouse_B (1)/Effects_IP_SIM_BoardGame");
            MeshRenderer sampleMesh = sampleBoardGame.GetComponent<MeshRenderer>();
            _highlightMaterials = [sampleMesh.materials[0], sampleMesh.materials[1]];
            _highlightShader = sampleMesh.material.shader;
            _hasCachedData = true;

            // create object mimicking the functionality of ship's CockpitAttachPoint
            _cockpitClone = new GameObject();
            _cockpitClone.SetActive(false);
            _cockpitClone.layer = LayerMask.NameToLayer("AdvancedEffectVolume");
            CapsuleCollider col = _cockpitClone.AddComponent<CapsuleCollider>();
            col.height = 2;
            col.radius = 1f;
            col.isTrigger = true;

            _cockpitClone.AddComponent<PlayerAttachPoint>();
            InteractZone interactZone = _cockpitClone.AddComponent<InteractZone>();
            interactZone._textID = (UITextType)Translations.GetUITextType("IC_INTERACT");
            // initialize screen prompts and trigger volume
            interactZone.Awake();

            DontDestroyOnLoad(_cockpitClone);
            Logger.Log("Finished caching objects");
        }

        private IEnumerator CreateGameSeat(Transform parent, Vector3 localPos, Quaternion localRot)
        {
            GameObject gameSeat = Instantiate(_cockpitClone, parent);
            gameSeat.transform.localPosition = localPos;
            gameSeat.transform.localRotation = localRot;
            gameSeat.SetActive(true);
            _attachPoint = gameSeat.GetComponent<PlayerAttachPoint>();
            _seatInteract = gameSeat.GetComponent<InteractZone>();
            // wait for Locator to be ready since its used in PlayerAttachPoint's Start()
            yield return new WaitUntil(() => Locator.GetPlayerBody() != null);
            // even though it should be enabled by default, have to do this explicitly to trigger Start()
            // this solves a bug where AttachPlayer()'s setting enabled would be overridden
            // by Start() being triggered right after (before instantiated object's first frame update)
            _attachPoint.enabled = true;
        }

        private void EnterOverheadView()
        {
            // my ability to directly lift mobius' code grows stronger with every passing day
            PlayerState = ChessPlayerState.EnteringOverhead;
            _initOverheadTime = Time.time;
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Lean, false);

            _playerCamController.SnapToDegreesOverSeconds(0f, -48.5f, 0.5f, true);
            _playerCamController.SnapToFieldOfView(24f, 0.5f, true);
            OWInput.ChangeInputMode(InputMode.Map);
        }

        private void ExitOverheadView()
        {
            PlayerState = ChessPlayerState.ExitingOverhead;
            _exitOverheadTime = Time.time;
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, true);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Lean, true);

            _cameraAPI.ExitCamera(_overheadCamController.OverheadCam);
            _overheadCamController.ResetPosition();
            _playerCamController.CenterCameraOverSeconds(0.5f, true);
            _playerCamController.SnapToInitFieldOfView(0.5f, true);
            OWInput.ChangeInputMode(InputMode.Character);
        }

        private void CompleteStandingUp()
        {
            _attachPoint.DetachPlayer();
            _seatInteract.ResetInteraction();
            _seatInteract.EnableInteraction();
            PrisonerSequence.EnableConversation();
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Score, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Overhead, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Lean, false);
            PlayerState = ChessPlayerState.None;
            OnStandUp?.Invoke();
        }

        public float GetLean()
        {
            return _leanAmt;
        }

        private void CheckAndFireLeanSFX()
        {
            float leanThreshold = _maxLeanAmt / 3;
            bool playedSound = false;

            if (Time.time > _lastLeanSoundTime + _leanSoundCooldown)
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

        private void OnEnterDreamworld()
        {
            if (_bgController.PlayerManip == null)
            {
                _bgController.PlayerManip = Locator.GetPlayerTransform().GetComponentInChildren<FirstPersonManipulator>();
                _playerCamController = Locator.GetPlayerCameraController();
                (OWCamera owCam, _) = _cameraAPI.CreateCustomCamera("Overhead Camera");
                Transform overhead = owCam.transform;
                overhead.SetParent(BoardGame.transform);
                overhead.localPosition = new Vector3(0f, 2f, 0);
                overhead.localRotation = Quaternion.Euler(90, 270, 0);
                _overheadCamController = overhead.gameObject.AddComponent<OverheadCameraController>();
                _overheadCamController.Setup();
            }
        }

        private void OnPressInteract()
        {
            _attachPoint.AttachPlayer();
            _seatInteract.DisableInteraction();
            PrisonerSequence.DisableConversation();
            (int won, int lost) = _bgController.GetScore();
            _screenPrompts.SetScore(won, lost);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Score, true);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, true);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Overhead, true);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Lean, true);
            PlayerState = ChessPlayerState.Seated;
            OnSitDown?.Invoke();
        }

        private static ICPrefabs LoadPrefabs(AssetBundle bundle, string bundlePath)
        {
            GameObject LoadAtPath(string prefabName) { return LoadPrefab(bundle, bundlePath + prefabName); }

            return new ICPrefabs
            {
                chess = LoadAtPath("boardgame.prefab"),
                space = LoadAtPath("boardgame_spacehighlight.prefab"),
                blocker = LoadAtPath("boardgame_blocker.prefab"),
                antler = LoadAtPath("boardgame_antler.prefab"),
                eye = LoadAtPath("boardgame_eye.prefab")
            };
        }

        // borrowed from https://github.com/Vesper-Works/OuterWildsHalf-Life/blob/main/HalfLifeOverhaul
        private static GameObject LoadPrefab(AssetBundle bundle, string path)
        {
            GameObject prefab = null;
            try
            {
                prefab = bundle.LoadAsset<GameObject>(path);

                // Repair materials             
                foreach (var skinnedMeshRenderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    foreach (var mat in skinnedMeshRenderer.materials)
                    {
                        mat.shader = s_standardShader;
                        //mat.renderQueue = 2000;
                    }
                }

                prefab.SetActive(false);
            }
            catch (Exception)
            {
                Logger.LogError($"Couldn't load {path}");
            }

            return prefab;
        }

        private TextAsset LoadText(string path)
        {
            string raw = File.ReadAllText(Path.Combine(ModHelper.Manifest.ModFolderPath, path));
            return new TextAsset(raw);
        }
    }
}
