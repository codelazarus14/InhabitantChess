using HarmonyLib;
using InhabitantChess.Util;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections;
using System.Collections.Generic;
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

        private float _exitSeatTime, _initOverheadTime, _exitOverheadTime;
        private BoardGameController _bgController;
        private PlayerCameraController _playerCamController;
        private OWCamera _overheadCamera;
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
            AssetBundle bundle = ModHelper.Assets.LoadBundle("Assets/triboard");
            LoadPrefabs(bundle, "assets/prefabs/triboard/");

            Logger.LogSuccess($"My mod {nameof(InhabitantChess)} is loaded!");

            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene != OWScene.SolarSystem) return;
                var playerBody = FindObjectOfType<PlayerBody>();

                PlayerState = ChessPlayerState.None;

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

                GameObject overhead = Instantiate(new GameObject("Overhead Camera"), BoardGame.transform);
                overhead.transform.localPosition = new Vector3(-0.1f, 1.5f, 0);
                overhead.transform.localRotation = Quaternion.Euler(90, 270, 0);
                _overheadCamera = overhead.AddComponent<OWCamera>();
                _overheadCamera.aspect = 1.6f;
                // TODO: fix culling, view lock on player camera (update patches?)
                _overheadCamera.enabled = false;
                // GameCamera and prompt text depend on Locator - have to wait a little longer
                StartCoroutine(LateInit());
            };
        }

        private IEnumerator LateInit(float delay = 1)
        {
            yield return new WaitForSeconds(delay);

            _bgController.PlayerManip = Locator.GetPlayerTransform().GetComponentInChildren<FirstPersonManipulator>();
            _playerCamController = Locator.GetPlayerCameraController();
            var flashbackEffect = _overheadCamera.transform.gameObject.AddComponent<FlashbackScreenGrabImageEffect>();
            var playerFlashEffect = _playerCamController.transform.GetComponent<FlashbackScreenGrabImageEffect>();
            flashbackEffect._downsampleShader = playerFlashEffect._downsampleShader;
            flashbackEffect._downsampleMaterial = playerFlashEffect._downsampleMaterial;
            _seatInteract.SetPromptText(UITextType.ItemUnknownArtifactPrompt);
            _seatInteract.OnPressInteract += this.OnPressInteract;

            Logger.Log("Finished setup");
        }

        private void OnPressInteract()
        {
            if (PlayerState == ChessPlayerState.None)
            {
                _seatInteract.DisableInteraction();
                _attachPoint.AttachPlayer();
                _bgController.EnterGame();
                PlayerState = ChessPlayerState.Seated;
            }
        }

        private void CompleteStandingUp()
        {
            _attachPoint.DetachPlayer();
            _seatInteract.ResetInteraction();
            _seatInteract.EnableInteraction();
            PlayerState = ChessPlayerState.None;
        }

        private void EnterOverheadView()
        {
            // my ability to directly lift mobius' code grows stronger with every passing day
            PlayerState = ChessPlayerState.EnteringOverhead;
            _initOverheadTime = Time.time;
            _playerCamController.SnapToDegreesOverSeconds(0f, -48.5f, 0.5f, true);
            _playerCamController.SnapToFieldOfView(24f, 0.5f, true);
        }

        private void ExitOverheadView()
        {
            PlayerState = ChessPlayerState.ExitingOverhead;
            _exitOverheadTime = Time.time;
            _overheadCamera.enabled = false;
            _playerCamController._playerCamera.enabled = true;
            _playerCamController.CenterCameraOverSeconds(0.5f, true);
            _playerCamController.SnapToInitFieldOfView(0.5f, true);
            GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", _playerCamController._playerCamera);
        }

        private void UpdateEnterOverheadTransition()
        {
            if (Time.time > _initOverheadTime + 0.45f) 
            {
                PlayerState = ChessPlayerState.InOverhead;
                _playerCamController._playerCamera.enabled = false;
                _overheadCamera.enabled = true;
                GlobalMessenger<OWCamera>.FireEvent("SwitchActiveCamera", _overheadCamera);
            }
        }

        private void Update()
        {
            if (_seatInteract == null || PlayerState == ChessPlayerState.None) return;

            if (PlayerState == ChessPlayerState.Seated && OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
            {
                //_bgController.ExitGame();
                Locator.GetPlayerCameraController().CenterCameraOverSeconds(0.2f, false);
                PlayerState = ChessPlayerState.StandingUp;
            }
            else if (PlayerState != ChessPlayerState.EnteringOverhead)
            {
                if (PlayerState != ChessPlayerState.InOverhead && OWInput.IsNewlyPressed(InputLibrary.landingCamera, InputMode.All))
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
            else if (PlayerState == ChessPlayerState.EnteringOverhead)
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
    }
}
