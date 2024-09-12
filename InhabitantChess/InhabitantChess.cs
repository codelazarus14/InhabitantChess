using HarmonyLib;
using InhabitantChess.Util;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// TODO: eyes of the past branch
// - same w prisoner sequence - make some generic class for wrapping dialogue/scripted sequences around setting up and clearing away the game
// - interacting with the board - prompt user to interact w spaces (highlight glows brighter)
// - display rules (ask the inhabitant diff options thru dialogue, optional "can i review the rules")
// - difficulty options (per game/instance), parameter for decision making, dialogue option

namespace InhabitantChess
{
    public class InhabitantChess : ModBehaviour
    {
        public static InhabitantChess Instance { get; private set; }

        public struct ICPrefabs
        {
            public GameObject chess;
            public GameObject space;
            public GameObject blocker;
            public GameObject antler;
            public GameObject eye;
        }
        public ICPrefabs Prefabs { get; private set; }

        public Material[] HighlightMaterials { get; private set; }
        public GameObject CockpitClone { get; private set; }
        public ChessGame CurrentGame { get; private set; }
        public PrisonerSequence PrisonerSequence { get; private set; }
        public Shortcut Shortcut { get; private set; }
        public ICommonCameraAPI CameraAPI { get; private set; }
        public (bool moves, bool pieces, bool beam) HighlightSettings { get; private set; }
        public bool ShortcutEnabled { get; private set; }

        public delegate void ConfigureEvent();
        public ConfigureEvent OnConfigure;

        private class ICData
        {
            public bool unlockedShortcut;
        }
        private ICData _saveData;

        private const string SaveFileName = "ic_save.json";
        private static Shader s_standardShader = Shader.Find("Standard");

        private List<ChessGame> _chessGames;
        private TextAsset _prisonerDialogue;
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
                Util.Logger.LogError("EOTE not detected - disabling InhabitantChess :(");
                enabled = false;
                return;
            }
            bool instancing = GetDependants().Count > 0;
            if (instancing)
                Util.Logger.Log($"Dependencies detected - enabling chess game instancing");

            // TODO testing - delete later
            instancing = true;

            CameraAPI = ModHelper.Interaction.TryGetModApi<ICommonCameraAPI>("xen.CommonCameraUtility");
            AssetBundle bundle = ModHelper.Assets.LoadBundle("Assets/triboard");
            Prefabs = LoadPrefabs(bundle, "assets/prefabs/triboard/");
            _prisonerDialogue = LoadText("Assets/PrisonerDialogue.xml");
            Translations.LoadTranslations();

            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                bool inSolarSystem = loadScene == OWScene.SolarSystem;
                if (inSolarSystem && !_hasCachedData)
                    CacheExistingData();
                else if (!_hasCachedData)
                {
                    Util.Logger.LogError($"Solar System scene hasn't been loaded - data is missing!");
                    return;
                }

                _chessGames = [];
                gameObject.GetAddComponent<ScreenPromptController>();

                TextTranslation.Get().OnLanguageChanged += Translations.OnLanguageChanged;

                Util.Logger.LogSuccess("Finished setup");

                if (!instancing && inSolarSystem)
                    InitPrisonerSequence();
            };
        }

        private void OnDestroy()
        {
            TextTranslation.Get().OnLanguageChanged -= Translations.OnLanguageChanged;
            foreach (ChessGame chess in _chessGames)
                Destroy(chess.gameObject);
            _chessGames.Clear();
            if (PrisonerSequence != null)
                PrisonerSequence.OnCleanupGame -= OnCleanupGame;
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

        public ChessGame InstantiateChessGame(Transform parent, Pose pose)
        {
            ChessGame chess = Instantiate(Prefabs.chess, parent).AddComponent<ChessGame>();
            chess.gameObject.SetActive(true);
            chess.transform.localPosition = pose.position;
            chess.transform.localRotation = pose.rotation;
            chess.OnSitDown += OnSitDown;
            chess.OnStoodUp += OnStoodUp;
            _chessGames.Add(chess);
            return chess;
        }

        private void CacheExistingData()
        {
            GameObject sampleBoardGame = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_1/Simulation_DreamZone_1/Props_DreamZone_1/Props_GenericHouse_B (1)/Effects_IP_SIM_BoardGame");
            MeshRenderer sampleMesh = sampleBoardGame.GetComponent<MeshRenderer>();
            HighlightMaterials = [sampleMesh.sharedMaterials[0], sampleMesh.sharedMaterials[1]];

            // create object mimicking the functionality of ship's CockpitAttachPoint
            CockpitClone = new GameObject();
            CockpitClone.SetActive(false);
            CockpitClone.layer = LayerMask.NameToLayer("AdvancedEffectVolume");
            CapsuleCollider col = CockpitClone.AddComponent<CapsuleCollider>();
            col.height = 2;
            col.radius = 1f;
            col.isTrigger = true;

            CockpitClone.AddComponent<PlayerAttachPoint>();
            InteractZone interactZone = CockpitClone.AddComponent<InteractZone>();
            interactZone._textID = (UITextType)Translations.GetUITextType("IC_INTERACT");
            // initialize screen prompts and trigger volume
            interactZone.Awake();

            _hasCachedData = true;
            DontDestroyOnLoad(CockpitClone);
            Util.Logger.Log("Finished caching objects");
        }

        private void InitPrisonerSequence()
        {
            GameObject prisonCell = GameObject.Find("DreamWorld_Body/Sector_DreamWorld/Sector_Underground/Sector_PrisonCell");

            PrisonerSequence = prisonCell.AddComponent<PrisonerSequence>();
            PrisonerSequence.SetText(_prisonerDialogue);
            Shortcut = prisonCell.AddComponent<Shortcut>();

            PrisonerSequence.OnCleanupGame += OnCleanupGame;
        }

        private void OnSitDown(ChessGame chess)
        {
            CurrentGame = chess;
            PrisonerSequence.DisableConversation();
        }

        private void OnStoodUp(ChessGame chess)
        {
            CurrentGame = null;
            PrisonerSequence.EnableConversation();
        }

        private void OnCleanupGame()
        {
            if (CurrentGame != null)
            CurrentGame.ForceStandUp();
            // override behavior of OnStoodUp()
            PrisonerSequence.DisableConversation();

            Util.Logger.Log("Player unlocked shortcut!");
            if (!_saveData.unlockedShortcut) _saveData.unlockedShortcut = true;
            ModHelper.Storage.Save(_saveData, SaveFileName);

            ShortcutEnabled = _saveData.unlockedShortcut && ModHelper.Config.GetSettingsValue<bool>("Enable Shortcut");
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
                Util.Logger.LogError($"Couldn't load {path}");
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
