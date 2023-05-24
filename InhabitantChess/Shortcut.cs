using InhabitantChess.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InhabitantChess
{
    public class Shortcut : MonoBehaviour
    {
        // modified version of https://github.com/VioVayo/OWDreamWorldModAssist/blob/main/DWModAssist/DWModAssist.cs

        public static Campfire LastUsedCampfire;
        public static NomaiRemoteCameraPlatform LastUsedProjectionPool;
        public static SlideProjector LastUsedSlideProjector;
        public static Peephole LastUsedPeephole;
        public static PlayerAttachPoint LastAttachedPoint;
        public DreamLanternItem Lantern;
        public bool UsedShortcut;

        private static RelativeLocationData _prisonLocationData, _campfireLocationData;
        private static ShipCockpitController _cockpitController;
        private static GameObject _itemDropSocket;
        private static DreamCampfire _campfire;
        private static DreamArrivalPoint _arrivalPoint;
        private static PrisonCellElevator _cellevator;
        private static SarcophagusController _vaultController;
        private static OWTriggerVolume _zone4PrisonCell, _zone4PrisonCellAir;
        private static DreamObjectProjector _lock1Projector, _lock2Projector, _lock3Projector;
        private static PrisonerSequence _sequence;
        private static MeshCollider _chairCollider;
        private static SingularityController _singularityController;

        private void Start()
        {
            FindReferences();
            _prisonLocationData = new(new Vector3(-77, -377.1f, 0), new Quaternion(0, 0.7f, 0, 0.7f), Vector3.zero); //PrisonerCell
            _campfireLocationData = new(new Vector3(0, 10.6f, 0), new Quaternion(0, 0, 0, 1), Vector3.zero); //DreamFireHouse

            _singularityController.transform.SetParent(Locator.GetPlayerBody().transform);
            _singularityController.transform.localPosition = new Vector3(0, 0.5f, 0.5f);
            _singularityController.enabled = true;

            GameObject slate = GameObject.Find("Characters_StartingCamp/Villager_HEA_Slate");
            Lantern.transform.SetParent(slate.transform);
            Lantern.transform.localPosition = new Vector3(-1.33f, 0.45f, -1.28f);
            Lantern.transform.localRotation = Quaternion.Euler(0, 300, 0);
            Lantern.SetSector(Lantern.GetComponentInParent<Sector>());
            Lantern.onPickedUp += EngageWarp;
        }

        private void FindReferences()
        {
            InhabitantChess ic = InhabitantChess.Instance;
            Lantern = GameObject.Find("Prefab_IP_DreamLanternItem_2").GetComponent<DreamLanternItem>();
            _itemDropSocket = new("ItemDropSocket");
            _itemDropSocket.transform.SetParent(GameObject.Find("Sector_DreamWorld").transform);
            _cellevator = FindObjectOfType<PrisonCellElevator>();
            _vaultController = FindObjectOfType<SarcophagusController>();
            _sequence = ic.PrisonerSequence;
            _chairCollider = ic.PrisonCell.FindChild("Props_PrisonCell/LowerCell/Prefab_IP_DW_Chair").GetComponent<MeshCollider>();
            GameObject hole = GameObject.Find("Sector_TH/Sector_NomaiCrater/Interactables_NomaiCrater/Prefab_NOM_WarpReceiver/BlackHole");
            _singularityController = Instantiate(hole).GetComponentInChildren<SingularityController>();

            var volumes = FindObjectsOfType<OWTriggerVolume>();
            foreach (var v in volumes)
            {
                var name = v.gameObject.name;
                if (name == "Sector_PrisonCell") _zone4PrisonCell = v;
                else if (name == "WaterOverrideVolume") _zone4PrisonCellAir = v;
            }

            var projectors = FindObjectsOfType<DreamObjectProjector>();
            foreach (var p in projectors)
            {
                var name = p.gameObject.name;
                if (name == "Prefab_IP_DreamObjectProjector (4)") _lock1Projector = p;
                else if (name == "Prefab_IP_DreamObjectProjector (3)") _lock2Projector = p;
                else if (name == "Prefab_IP_DreamObjectProjector (2)") _lock3Projector = p;
            }

            _cockpitController = FindObjectOfType<ShipCockpitController>();
        }

        private void EngageWarp(OWItem item)
        {
            Lantern.onPickedUp -= EngageWarp;

            UsedShortcut = true;
            _sequence.PrisonerDirector.InitializeSequence();
            GetPrisonerUpToSpeed();
            StartCoroutine(WarpToPrison(2));
            StartCoroutine(WaitForFinalSetup());
            OpenVault();
        }

        private void GetPrisonerUpToSpeed()
        {
            PrisonerDirector director = _sequence.PrisonerDirector;
            CharacterDialogueTree dialogue = _sequence.PrisonerDialogue;
            // remove anachronistic listeners, add one for exiting game via dialogue
            dialogue.OnEndConversation -= director.OnFinishDialogue;
            dialogue.OnEndConversation += _sequence.OnFinishGameDialogue;
            director._prisonerBrain.OnArriveAtElevatorDoor -= director.OnPrisonerArriveAtElevatorDoor;
            director._prisonerBrain.OnArriveAtElevatorDoor -= _sequence.OnArriveAtElevator;
            director._prisonerEffects.OnReadyToReceiveTorch -= _sequence.OnReadyForTorch;
            _sequence.TorchSocket.OnSocketablePlaced = (OWItemSocket.SocketEvent)Delegate.Remove(_sequence.TorchSocket.OnSocketablePlaced, 
                                                                    new OWItemSocket.SocketEvent(_sequence.OnPlayerPlaceTorch));
            // remove darkness plane, give lantern, turn on lights
            director._darknessAwoken = true;
            director._prisonerController._lantern.SetConcealed(true);
            director._prisonerController.MoveLanternToCarrySocket(true, 0.5f);
            director.OnPrisonerLitLights();
            // this is true by default
            director._waitingForPlayerToTakeTorch = false;
            // OnArriveAtElevator dialogue setup
            dialogue.SetTextXml(_sequence.DialogueText);
            Translations.UpdateCharacterDialogue(dialogue);
            _sequence.UpdatePromptText();
            // skip first part of dialogue at the elevator
            DialogueConditionManager.SharedInstance.SetConditionState("IC_TALKED_TO_PRISONER", true);
            // place torch into socket so it moves with it
            _sequence.TorchSocket.PlaceIntoSocket(director._visionTorchItem);
            _sequence.TorchSocket.EnableInteraction(false);
            _sequence.enabled = true;
            // disable input to stop player from putting down lantern
            OWInput.ChangeInputMode(InputMode.None);
        }

        private IEnumerator WaitForFinalSetup()
        {
            while (!_chairCollider.enabled) yield return null;
            _sequence.SetUpGame(false);
        }

        private void GiveLantern(Vector3 worldDestinationPosition)
        {
            _itemDropSocket.transform.position = worldDestinationPosition;

            var itemTool = Locator.GetToolModeSwapper().GetItemCarryTool();
            if (Locator.GetToolModeSwapper().GetToolMode() != ToolMode.None)
            {
                if (Locator.GetToolModeSwapper().GetToolMode() != ToolMode.Item) Locator.GetToolModeSwapper().UnequipTool();
                else if (itemTool.GetHeldItemType() == ItemType.DreamLantern) return;
                else
                {
                    var item = itemTool.GetHeldItem();
                    itemTool.DropItemInstantly(Locator.GetDreamWorldController()._dreamWorldSector, _itemDropSocket.transform);
                    item.gameObject.transform.SetParent(_itemDropSocket.transform.parent, true);
                }
            }
            itemTool.PickUpItemInstantly(Lantern);
        }

        private IEnumerator ResetPlayerState()
        {
            if (LastUsedProjectionPool != null && LastUsedProjectionPool.IsPlatformActive())
            {
                LastUsedProjectionPool.OnLeaveBounds();
                while (PlayerState.UsingNomaiRemoteCamera()) yield return null;
            }
            if (LastUsedCampfire != null)
            {
                LastUsedCampfire.StopRoasting();
                LastUsedCampfire.StopSleeping();
            }
            if (PlayerState.AtFlightConsole()) _cockpitController.ExitFlightConsole();
            if (PlayerState.IsViewingProjector()) LastUsedSlideProjector.CancelInteraction();
            if (PlayerState.IsPeeping()) LastUsedPeephole.Unpeep();
            if (PlayerState.IsAttached()) LastAttachedPoint.DetachPlayer();
            Locator.GetPlayerTransform().GetRequiredComponent<PlayerLockOnTargeting>().BreakLock();
            OWInput.ChangeInputMode(InputMode.Character);

            PlayerState._isResurrected = false;
            Locator.GetDreamWorldController().ExitDreamWorld();
            while (Locator.GetDreamWorldController().IsInDream()) yield return null;
        }

        private IEnumerator WarpToPrison(float delay = 0)
        {
            // toggle black hole on player, make prompts disappear before actual warp
            yield return new WaitForSecondsRealtime(delay - 1);
            _singularityController.Create();
            yield return new WaitForSecondsRealtime(delay);

            // use safe fire so player doesn't get woken up
            _campfire = Locator.GetDreamCampfire(DreamArrivalPoint.Location.Zone3);
            _arrivalPoint = Locator.GetDreamArrivalPoint(DreamArrivalPoint.Location.Zone4);

            GiveLantern(_arrivalPoint.transform.TransformPoint(_prisonLocationData.localPosition - new Vector3(0, 0.5f, 0)));
            yield return ResetPlayerState();

            Locator.GetDreamWorldController().EnterDreamWorld(_campfire, _arrivalPoint, _prisonLocationData);
            yield return new WaitForFixedUpdate();
            Locator.GetDreamWorldController()._relativeSleepLocation.localPosition = _campfireLocationData.localPosition;

            // enterByDeath == true
            PlayerState._isResurrected = true;

            foreach (var volume in _arrivalPoint._entrywayVolumes) volume.RemoveAllObjectsFromVolume();
            List<OWTriggerVolume> volumes = new()
            {
                _zone4PrisonCell,
                _zone4PrisonCellAir,
                _vaultController._tunnelEntrywayTrigger
            };
            _vaultController._tunnelEntrywayTrigger.SetTriggerActivation(true);
            _cellevator.CallToBottomFloor();
            _cellevator.TryOpenDoor();

            foreach (var volume in volumes)
            {
                volume.AddObjectToVolume(Locator.GetPlayerDetector().gameObject);
                volume.AddObjectToVolume(Locator.GetPlayerCameraDetector().gameObject);
                volume.AddObjectToVolume(Lantern.GetFluidDetector().gameObject);
            }
            // turn off black hole
            yield return new WaitForSecondsRealtime(delay);
            _singularityController.Collapse();
        }

        private void OpenVault()
        {
            _lock1Projector.SetLit(false);
            _lock2Projector.SetLit(false);
            _lock3Projector.SetLit(false);
            _vaultController.OnPressInteract();
        }

        private void OnDestroy()
        {
            Lantern.onPickedUp -= EngageWarp;
        }
    }
}
