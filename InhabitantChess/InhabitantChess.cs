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
        public ChessPlayerState PlayerState { get; private set; }
        public float LeanDist { get; private set; }

        private float _exitSeatTime, _initOverheadTime, _exitOverheadTime;
        private float _maxLeanDist = 1f, _leanSpeed = 1.5f;
        private PrisonerBehavior _prisonerBehavior;
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
                Logger.LogError("HugMod detected - disabling mod :(");
                enabled = false;
                return;
            }
            
            AssetBundle bundle = ModHelper.Assets.LoadBundle("Assets/triboard");
            LoadPrefabs(bundle, "assets/prefabs/triboard/");
            TextAsset prisonerDialogue = LoadText("Assets/PrisonerDialogue.xml");

            Logger.LogSuccess($"My mod {nameof(InhabitantChess)} is loaded!");

            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene != OWScene.SolarSystem) return;

                PlayerState = ChessPlayerState.None;

                GameObject prisonCell = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_Underground/Sector_PrisonCell");
                // TODO: use for board game, copy original transforms to restore after game
                GameObject crate = prisonCell.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_Crate_Sealed (1)");
                GameObject uselessBoard = prisonCell.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_BoardGame");
                GameObject playerChair = prisonCell.FindChild("Props_PrisonCell/LowerCell/Prefab_IP_DW_Chair");
                GameObject prisonerChair = prisonCell.FindChild("Interactibles_PrisonCell/PrisonerSequence/Prefab_IP_DW_Chair");

                GameObject slate = GameObject.Find("Sector_TH/Sector_Village/Sector_StartingCamp/Characters_StartingCamp/Villager_HEA_Slate/Villager_HEA_Slate_ANIM_LogSit");
                BoardGame = Instantiate(_prefabDict["chessPrefab"], slate.transform);
                BoardGame.transform.localPosition = new Vector3(-5, 0.8f, 1.5f);
                BoardGame.SetActive(true);

                Synchronizer synch = BoardGame.AddComponent<Synchronizer>();
                BoardController bController = BoardGame.transform.Find("BoardGame_Board").gameObject.AddComponent<BoardController>();
                bController.SpacePrefab = _prefabDict["spacePrefab"];
                bController.BlockerPrefab = _prefabDict["blockerPrefab"];
                bController.AntlerPrefab = _prefabDict["antlerPrefab"];
                bController.EyePrefab = _prefabDict["eyePrefab"];
                bController.Synchronizer = synch;
                // TODO: glowy VP is a little too bright - can we tone it down?
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
                _attachPoint = gameAttach.GetComponent<PlayerAttachPoint>();
                _seatInteract = gameAttach.GetComponent<InteractZone>();

                GameObject gneissSeat = GameObject.Find("TimberHearth_Body/Sector_TH/Sector_Village/Sector_LowerVillage/Characters_LowerVillage/Villager_HEA_Gneiss/Villager_HEA_Gneiss_ANIM_Tuning/Villager_HEA_Gneiss_ANIM_Rocker/Props_HEA_RockingChair:Props_HEA_RockingChair");
                GameObject gameSeat = Instantiate(gneissSeat, BoardGame.transform);
                gameSeat.transform.localPosition = new Vector3(1, -0.8f, 0);
                gameSeat.transform.localRotation = Quaternion.Euler(0, 270, 0);

                _screenPrompts = BoardGame.AddComponent<ScreenPrompts>();

                _prisonerBehavior = BoardGame.AddComponent<PrisonerBehavior>();
                _prisonerBehavior.SetText(prisonerDialogue);

                TextTranslation.Get().OnLanguageChanged += Translations.UpdateLanguage;

                // camera init, interact prompt on Locator - have to wait a little longer
                StartCoroutine(LateInit());
            };
        }

        private IEnumerator LateInit(float delay = 1)
        {
            yield return new WaitForSeconds(delay);

            _bgController.PlayerManip = Locator.GetPlayerTransform().GetComponentInChildren<FirstPersonManipulator>();
            _playerCamController = Locator.GetPlayerCameraController();
            (OWCamera owCam, Camera cam) customCamera = _cameraAPI.CreateCustomCamera("Overhead Camera");
            Transform overhead = customCamera.owCam.transform;
            overhead.SetParent(BoardGame.transform);
            overhead.localPosition = new Vector3(0f, 2f, 0);
            overhead.localRotation = Quaternion.Euler(90, 270, 0);
            _overheadCamController = overhead.gameObject.AddComponent<OverheadCameraController>();
            _overheadCamController.Setup();

            _seatInteract.SetPromptText((UITextType)Translations.GetUITextType("IC_INTERACT"));
            _seatInteract.OnPressInteract += OnPressInteract;

            Logger.Log("Finished setup");
        }

        private void OnPressInteract()
        {
            if (PlayerState == ChessPlayerState.None)
            {
                _seatInteract.DisableInteraction();
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

        private void Update()
        {
            if (_seatInteract == null || PlayerState == ChessPlayerState.None) return;

            if (PlayerState == ChessPlayerState.Seated)
            {
                if (OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
                {
                    //_bgController.ExitGame();
                    LeanDist = 0f;
                    _playerCamController.CenterCameraOverSeconds(0.2f, false);
                    PlayerState = ChessPlayerState.StandingUp;
                } 
                else if (OWInput.IsPressed(InputLibrary.moveXZ, InputMode.All))
                {
                    float v = OWInput.GetAxisValue(InputLibrary.moveXZ).y;
                    LeanDist += v *_leanSpeed * Time.deltaTime;
                    LeanDist = Mathf.Clamp(LeanDist, 0.0f, _maxLeanDist);
                    // update position ig?
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
