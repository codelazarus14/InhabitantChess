using HarmonyLib;
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

        private BoardGameController _bgController;
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
                // GameCamera and prompt text depend on Locator - have to wait a little longer
                StartCoroutine(LateInit());
            };
        }

        private IEnumerator LateInit(float delay = 1)
        {
            yield return new WaitForSeconds(delay);

            _bgController.GameCamera = Locator.GetActiveCamera().mainCamera;
            _seatInteract.SetPromptText(UITextType.ItemUnknownArtifactPrompt);
            _seatInteract.OnPressInteract += this.OnPressInteract;

            Logger.Log("Finished setup");
        }

        private void OnPressInteract()
        {
            _seatInteract.DisableInteraction();
            _attachPoint.AttachPlayer();
            _bgController.EnterGame();
        }

        private void Update()
        {
            if (_seatInteract == null) return;
            else if (_bgController.Playing && OWInput.IsNewlyPressed(InputLibrary.cancel, InputMode.All))
            {
                _bgController.ExitGame();
                _attachPoint.DetachPlayer();
                _seatInteract.ResetInteraction();
                _seatInteract.EnableInteraction();
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
