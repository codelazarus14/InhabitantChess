using HarmonyLib;
using InhabitantChess.BoardGame;
using InhabitantChess.Util;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections;
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
        public ChessPlayerState PlayerState { get; private set; }

        private float _exitSeatTime, _initOverheadTime, _exitOverheadTime;
        private float _leanDist, _maxLeanDist = 1f, _leanSpeed = 1.5f;
        private bool _initialized;
        private PrisonerSequence _prisonerSequence;
        private BoardGameController _bgController;
        private ICommonCameraAPI _cameraAPI;
        private PlayerCameraController _playerCamController;
        private OverheadCameraController _overheadCamController;
        private ScreenPrompts _screenPrompts;
        private PlayerAttachPoint _attachPoint;
        private InteractZone _seatInteract;
        private Dictionary<string, GameObject> _prefabDict = new();
        private static Shader s_standardShader = Shader.Find("Standard");

        private void Awake()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        private void Start()
        {
            //TODO: determine if first encounter - ModHelper.Storage.Load<ICData>("ic_save.json");

            _cameraAPI = ModHelper.Interaction.TryGetModApi<ICommonCameraAPI>("xen.CommonCameraUtility");
            // hug mod messes with the prisoner's InteractReceiver - disable it for now
            if (ModHelper.Interaction.ModExists("VioVayo.HugMod"))
            {
                Logger.LogError("HugMod detected - disabling InhabitantChess :(");
                enabled = false;
                return;
            }
            
            AssetBundle bundle = ModHelper.Assets.LoadBundle("Assets/triboard");
            LoadPrefabs(bundle, "assets/prefabs/triboard/");
            TextAsset prisonerDialogue = LoadText("Assets/PrisonerDialogue.xml");

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
                _bgController.StartText = BoardGame.transform.Find("StartText").gameObject;

                GameObject cockpitAttach = GameObject.Find("Ship_Body/Module_Cockpit/Systems_Cockpit/CockpitAttachPoint");
                GameObject gameAttach = Instantiate(cockpitAttach, BoardGame.transform);
                gameAttach.transform.localPosition = new Vector3(1, 0, 0);
                gameAttach.transform.localRotation = Quaternion.Euler(0, 270, 0);
                gameAttach.GetComponent<CapsuleCollider>().radius *= 2;
                _attachPoint = gameAttach.GetComponent<PlayerAttachPoint>();
                _seatInteract = gameAttach.GetComponent<InteractZone>();
                _seatInteract._textID = (UITextType) Translations.GetUITextType("IC_INTERACT");
                // default screen prompts not initialized yet? so we have to create our own
                _seatInteract.Awake();

                GameObject gneissSeat = GameObject.Find("TimberHearth_Body/Sector_TH/Sector_Village/Sector_LowerVillage/Characters_LowerVillage/Villager_HEA_Gneiss/Villager_HEA_Gneiss_ANIM_Tuning/Villager_HEA_Gneiss_ANIM_Rocker/Props_HEA_RockingChair:Props_HEA_RockingChair");
                GameObject gameSeat = Instantiate(gneissSeat, BoardGame.transform);
                gameSeat.transform.localPosition = new Vector3(1, -0.8f, 0);
                gameSeat.transform.localRotation = Quaternion.Euler(0, 270, 0);

                _screenPrompts = BoardGame.AddComponent<ScreenPrompts>();
                _prisonerSequence = PrisonCell.gameObject.AddComponent<PrisonerSequence>();
                _prisonerSequence.SetText(prisonerDialogue);

                _seatInteract.OnPressInteract += OnPressInteract;
                TextTranslation.Get().OnLanguageChanged += Translations.UpdateLanguage;
                // set up camera w util later
                GlobalMessenger.AddListener("EnterDreamWorld", new Callback(OnEnterDreamworld));

                Logger.LogSuccess("Finished setup");
            };
        }

        private void OnEnterDreamworld()
        {
            if (!_initialized)
            {
                _initialized = true;
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
            if (PlayerState == ChessPlayerState.None)
            {
                _seatInteract.DisableInteraction();
                _prisonerSequence.DisableConversation();
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, true);
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Overhead, true);
                _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.LeanForward, true);
                _attachPoint.AttachPlayer();
                _bgController.EnterGame();
                PlayerState = ChessPlayerState.Seated;
            }
        }

        private void CompleteStandingUp()
        {
            _attachPoint.DetachPlayer();
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.Overhead, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.LeanForward, false);
            _seatInteract.ResetInteraction();
            _seatInteract.EnableInteraction();
            _prisonerSequence.EnableConversation();
            PlayerState = ChessPlayerState.None;
        }

        private void EnterOverheadView()
        {
            // my ability to directly lift mobius' code grows stronger with every passing day
            PlayerState = ChessPlayerState.EnteringOverhead;
            _initOverheadTime = Time.time;
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, false);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.LeanForward, false);

            _playerCamController.SnapToDegreesOverSeconds(0f, -48.5f, 0.5f, true);
            _playerCamController.SnapToFieldOfView(24f, 0.5f, true);
            OWInput.ChangeInputMode(InputMode.Map);
        }

        private void ExitOverheadView()
        {
            PlayerState = ChessPlayerState.ExitingOverhead;
            _exitOverheadTime = Time.time;
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.BoardMove, true);
            _screenPrompts.SetPromptVisibility(ScreenPrompts.PromptType.LeanForward, true);

            _cameraAPI.ExitCamera(_overheadCamController.OverheadCam);
            _overheadCamController.SetEnabled(false);
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
                _overheadCamController.SetEnabled(true);
            }
        }

        public float GetLean()
        {
            return _leanDist;
        }

        private void Update()
        {
            if (_seatInteract == null || PlayerState == ChessPlayerState.None) return;

            if (PlayerState == ChessPlayerState.Seated)
            {
                if (OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
                {
                    //_bgController.ExitGame();
                    _leanDist = 0f;
                    _playerCamController.CenterCameraOverSeconds(0.2f, false);
                    PlayerState = ChessPlayerState.StandingUp;
                } 
                else if (OWInput.IsPressed(InputLibrary.moveXZ, InputMode.All))
                {
                    float v = OWInput.GetAxisValue(InputLibrary.moveXZ).y;
                    _leanDist += v *_leanSpeed * Time.deltaTime;
                    _leanDist = Mathf.Clamp(_leanDist, 0.0f, _maxLeanDist);
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
            _seatInteract.OnPressInteract -= OnPressInteract;
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

        //code below borrowed from https://github.com/Vesper-Works/OuterWildsHalf-Life/blob/main/HalfLifeOverhaul
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
