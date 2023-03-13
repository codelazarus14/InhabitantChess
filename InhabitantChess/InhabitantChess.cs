using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    public class InhabitantChess : ModBehaviour
    {

        public static InhabitantChess Instance { get; private set; }
        public OWCamera GameCamera;
        public BoardGameController BGController { get; private set; }

        private Dictionary<string, GameObject> _prefabDict = new();
        private static Shader s_standardShader = Shader.Find("Standard");

        private void Awake()
        {
            // You won't be able to access OWML's mod helper in Awake.
            // So you probably don't want to do anything here.
            // Use Start() instead.
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
                GameObject chess = Instantiate(_prefabDict["chessPrefab"], slate.transform);
                chess.SetActive(true);
                chess.transform.localPosition = new Vector3(-5, 0.8f, 1.5f);
                Synchronizer synch = chess.AddComponent<Synchronizer>();
                BoardController bController = chess.transform.Find("BoardGame_Board").gameObject.AddComponent<BoardController>();
                bController.SpacePrefab = _prefabDict["spacePrefab"];
                bController.BlockerPrefab = _prefabDict["blockerPrefab"];
                bController.AntlerPrefab = _prefabDict["antlerPrefab"];
                bController.EyePrefab = _prefabDict["eyePrefab"];
                bController.Synchronizer = synch;
                BGController = chess.AddComponent<BoardGameController>();
                BGController.StartText = chess.transform.Find("StartText").gameObject;

                GameObject cockpitAttach = GameObject.Find("Ship_Body/Module_Cockpit/Systems_Cockpit/CockpitAttachPoint");
                GameObject gameAttach = Instantiate(cockpitAttach, chess.transform);
                gameAttach.transform.localPosition = new Vector3(1, 0, 0);
                gameAttach.transform.localRotation = Quaternion.Euler(0, 270, 0);
                GameObject gneissSeat = GameObject.Find("TimberHearth_Body/Sector_TH/Sector_Village/Sector_LowerVillage/Characters_LowerVillage/Villager_HEA_Gneiss/Villager_HEA_Gneiss_ANIM_Tuning/Villager_HEA_Gneiss_ANIM_Rocker/Props_HEA_RockingChair:Props_HEA_RockingChair");
                GameObject gameSeat = Instantiate(gneissSeat, chess.transform);
                gameSeat.transform.localPosition = new Vector3(1, -0.8f, 0);
                gameSeat.transform.localRotation = Quaternion.Euler(0, 270, 0);
                // GameCamera, BoardGame tags and prompt text depend on Locator - have to wait a little longer


                Logger.Log("Finished Start");
            };
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
