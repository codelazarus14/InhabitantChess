using HarmonyLib;
using InhabitantChess.BoardGame;
using InhabitantChess.Util;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

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
        public (bool moves, bool pieces, bool beam) Highlighting { get; private set; }
        public bool ShortcutEnabled { get; private set; }

        public delegate void ChessPlayerAudioEvent();
        public ChessPlayerAudioEvent OnLeanForward;
        public ChessPlayerAudioEvent OnLeanBackward;
        public ChessPlayerAudioEvent OnSitDown;
        public ChessPlayerAudioEvent OnStandUp;

        private delegate void ConfigureEvent();
        private ConfigureEvent OnConfigure;

        private float _exitSeatTime, _initOverheadTime, _exitOverheadTime;
        private float _oldLeanAmt, _leanAmt, _lastLeanSoundTime, _maxLeanAmt = 1f, _leanSpeed = 1.5f, _leanSoundCooldown = 1f;
        private BoardGameController _bgController;
        private ICommonCameraAPI _cameraAPI;
        private PlayerCameraController _playerCamController;
        private OverheadCameraController _overheadCamController;
        private ScreenPrompts _screenPrompts;
        private PlayerAttachPoint _attachPoint;
        private InteractZone _seatInteract;
        private Dictionary<string, GameObject> _prefabDict = new();
        private const string SaveFileName = "ic_save.json";
        private static Shader s_standardShader = Shader.Find("Standard");
        private class ICData
        {
            public bool unlockedShortcut;
        }
        private ICData _saveData;

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

            AssetBundle bundle = ModHelper.Assets.LoadBundle("Assets/triboard");
            LoadPrefabs(bundle, "assets/prefabs/triboard/");
            TextAsset prisonerDialogue = LoadText("Assets/PrisonerDialogue.xml");
            Translations.LoadTranslations();

            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene != OWScene.SolarSystem) return;

                PlayerState = ChessPlayerState.None;

                PrisonCell = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_Underground/Sector_PrisonCell");

                BoardGame = Instantiate(_prefabDict["chessPrefab"], PrisonCell.transform);
                BoardGame.transform.localPosition = new Vector3(4, -35.105f, 0.2f);
                BoardGame.transform.localRotation = Quaternion.Euler(0, 270, 0);

                Synchronizer synch = BoardGame.AddComponent<Synchronizer>();
                BoardController bController = BoardGame.transform.Find("BoardGame_Board").gameObject.AddComponent<BoardController>();
                bController.SpacePrefab = _prefabDict["spacePrefab"];
                bController.BlockerPrefab = _prefabDict["blockerPrefab"];
                bController.AntlerPrefab = _prefabDict["antlerPrefab"];
                bController.EyePrefab = _prefabDict["eyePrefab"];
                bController.Synchronizer = synch;

                GameObject sampleBoardGame = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_1/Simulation_DreamZone_1/Props_DreamZone_1/Props_GenericHouse_B (1)/Effects_IP_SIM_BoardGame");
                MeshRenderer sampleMesh = sampleBoardGame.GetComponent<MeshRenderer>();
                Material dreamGridVP = sampleMesh.materials[0];
                Material dreamGrid = sampleMesh.materials[1];
                bController.HighlightShader = sampleMesh.material.shader;
                bController.HighlightMaterials = new Material[] { dreamGridVP, dreamGrid };
                _bgController = BoardGame.AddComponent<BoardGameController>();

                GameObject cockpitAttach = GameObject.Find("Ship_Body/Module_Cockpit/Systems_Cockpit/CockpitAttachPoint");
                GameObject gameAttach = Instantiate(cockpitAttach, BoardGame.transform);
                gameAttach.transform.localPosition = new Vector3(1, 0, 0);
                gameAttach.transform.localRotation = Quaternion.Euler(0, 270, 0);
                gameAttach.GetComponent<CapsuleCollider>().radius *= 2;
                _attachPoint = gameAttach.GetComponent<PlayerAttachPoint>();
                _seatInteract = gameAttach.GetComponent<InteractZone>();
                _seatInteract._textID = (UITextType)Translations.GetUITextType("IC_INTERACT");
                // default screen prompts not initialized yet? so we have to create our own
                _seatInteract.Awake();

                _screenPrompts = BoardGame.AddComponent<ScreenPrompts>();
                PrisonerSequence = PrisonCell.AddComponent<PrisonerSequence>();
                PrisonerSequence.SetText(prisonerDialogue);
                Shortcut = PrisonCell.AddComponent<Shortcut>();
                AudioEffects = PrisonCell.AddComponent<AudioEffects>();

                OnConfigure += () => _bgController.OnHighlightConfigure(Highlighting);
                OnConfigure += () => Shortcut.EnableShortcut(ShortcutEnabled);
                _seatInteract.OnPressInteract += OnPressInteract;
                TextTranslation.Get().OnLanguageChanged += Translations.UpdateLanguage;
                // set up camera w util later
                GlobalMessenger.AddListener("EnterDreamWorld", new Callback(OnEnterDreamworld));

                Logger.LogSuccess("Finished setup");
            };
        }

        public override void Configure(IModConfig config)
        {
            if (_saveData == null)
                _saveData = ModHelper.Storage.Load<ICData>(SaveFileName) ?? new();
            //Logger.Log($"Shortcut unlocked? {_saveData.unlockedShortcut}");

            ShortcutEnabled = /*_saveData.unlockedShortcut &&*/ config.GetSettingsValue<bool>("Enable Shortcut");
            Highlighting = new(config.GetSettingsValue<bool>("Moves Highlighting"),
                                config.GetSettingsValue<bool>("Piece Highlighting"),
                                config.GetSettingsValue<bool>("Beam Highlighting"));
            OnConfigure?.Invoke();
        }

        public void ShortcutUnlocked()
        {
            Logger.Log("Player unlocked shortcut!");
            if (!_saveData.unlockedShortcut) _saveData.unlockedShortcut = true;
            ModHelper.Storage.Save(_saveData, SaveFileName);

            ShortcutEnabled = _saveData.unlockedShortcut && ModHelper.Config.GetSettingsValue<bool>("Enable Shortcut");
        }

        private void OnEnterDreamworld()
        {
            if (_bgController.PlayerManip == null)
            {
                _bgController.PlayerManip = Locator.GetPlayerTransform().GetComponentInChildren<FirstPersonManipulator>();
                _playerCamController = Locator.GetPlayerCameraController();
                (OWCamera owCam, Camera cam) customCamera = _cameraAPI.CreateCustomCamera("Overhead Camera");
                Transform overhead = customCamera.owCam.transform;
                overhead.SetParent(BoardGame.transform);
                overhead.localPosition = new Vector3(0f, 2f, 0);
                overhead.localRotation = Quaternion.Euler(90, 270, 0);
                _overheadCamController = overhead.gameObject.AddComponent<OverheadCameraController>();
                _overheadCamController.Setup();
            }
        }

        private void OnPressInteract()
        {
            if (_overheadCamController == null || _playerCamController == null) return;

            _seatInteract.DisableInteraction();
            _bgController.OnInteract();
            // only update if not seated
            if (PlayerState == ChessPlayerState.None)
            {
                PrisonerSequence.DisableConversation();
                (int won, int lost) score = _bgController.GetScore();
                _screenPrompts.SetScore(score.won, score.lost);
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Score, true);
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, true);
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Overhead, true);
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Lean, true);
                _attachPoint.AttachPlayer();
                PlayerState = ChessPlayerState.Seated;
                OnSitDown?.Invoke();
            }
        }

        private void CompleteStandingUp()
        {
            _seatInteract.ResetInteraction();
            _seatInteract.EnableInteraction();
            PrisonerSequence.EnableConversation();
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Score, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Overhead, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Lean, false);
            _attachPoint.DetachPlayer();
            PlayerState = ChessPlayerState.None;
            OnStandUp?.Invoke();
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

        private void UpdateEnterOverheadTransition()
        {
            if (Time.time > _initOverheadTime + 0.45f)
            {
                PlayerState = ChessPlayerState.InOverhead;
                _cameraAPI.EnterCamera(_overheadCamController.OverheadCam);
                _overheadCamController.ResetPosition();
            }
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

        private void Update()
        {
            if (_seatInteract == null || PlayerState == ChessPlayerState.None) return;

            if (PlayerState == ChessPlayerState.Seated)
            {
                if (OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
                {
                    //_bgController.ExitGame();
                    _leanAmt = 0f;
                    _oldLeanAmt = 0f;
                    _playerCamController.CenterCameraOverSeconds(0.2f, false);
                    PlayerState = ChessPlayerState.StandingUp;
                    _exitSeatTime = Time.time;
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

        private void OnDestroy()
        {
            GlobalMessenger.RemoveListener("EnterDreamWorld", new Callback(OnEnterDreamworld));
            TextTranslation.Get().OnLanguageChanged -= Translations.UpdateLanguage;
            if (_seatInteract != null) _seatInteract.OnPressInteract -= OnPressInteract;
            OnConfigure -= () => Shortcut.EnableShortcut(ShortcutEnabled);
            OnConfigure -= () => _bgController.OnHighlightConfigure(Highlighting);
        }

        private void LoadPrefabs(AssetBundle bundle, string bundlePath)
        {
            (string label, string name)[] prefabs =
            {
                ("chessPrefab", "boardgame.prefab"),
                ("spacePrefab", "boardgame_spacehighlight.prefab"),
                ("blockerPrefab", "boardgame_blocker.prefab"),
                ("antlerPrefab", "boardgame_antler.prefab"),
                ("eyePrefab", "boardgame_eye.prefab")
            };

            for (int i = 0; i < prefabs.Length; i++)
            {
                _prefabDict.Add(prefabs[i].label, LoadPrefab(bundle, bundlePath + prefabs[i].name));
            }
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
