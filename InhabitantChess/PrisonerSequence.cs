using InhabitantChess.Util;
using System;
using System.Collections;
using UnityEngine;

namespace InhabitantChess
{
    public class PrisonerSequence : MonoBehaviour
    {
        public PrisonerDirector PrisonerDirector { get; private set; }
        public CharacterDialogueTree PrisonerDialogue { get; private set; }
        public TextAsset DialogueText { get; private set; }
        public VisionTorchSocket TorchSocket { get; private set; }
        public ChessGame ChessGame { get; private set; }

        public bool CanTriggerSequence;

        public delegate void PrisonerSequenceEvent();
        public PrisonerSequenceEvent OnSpotlightTorch;
        public PrisonerSequenceEvent OnPrisonerCurious;
        public PrisonerSequenceEvent OnSetupGame;
        public PrisonerSequenceEvent OnCleanupGame;

        private enum PrisonerState
        {
            None,
            ReactingToTorch,
            WalkingToMarker,
            BeingSeated,
            Seated,
            SaidFinalWords,
            BeingRestored,
            BackAtElevator
        }
        private PrisonerState _state;

        private struct Prop
        {
            public GameObject gameObject;
            public Pose ogPose;
            public Pose movedPose;

            public Prop(GameObject gO, Pose moved)
            {
                gameObject = gO;
                ogPose = new Pose(gO.transform.localPosition, gO.transform.localRotation);
                movedPose = moved;
            }

            public void Move()
            {
                gameObject.transform.localPosition = movedPose.position;
                gameObject.transform.localRotation = movedPose.rotation;
            }

            public void Reset()
            {
                gameObject.transform.localPosition = ogPose.position;
                gameObject.transform.localRotation = ogPose.rotation;
            }
        }

        private struct PrisonerProps
        {
            public Prop crate;
            public Prop emptyBoard;
            public Prop playerChair;
            public Prop prisonerChair;
            public Prop torchSocket;

            private static Pose[] s_movedPoses =
            [
                new Pose(new Vector3(4, 0.035f, 0), Quaternion.Euler(0, 180, 0)),
                new Pose(new Vector3(4, 0.85f, 0.2f), Quaternion.Euler(0, 270, 0)),
                new Pose(new Vector3(4, 0.035f, 1.75f), Quaternion.Euler(0, 180, 0)),
                new Pose(new Vector3(-0.75f, 0, 3.9f), Quaternion.Euler(0, 261.3473f, 0)),
                new Pose(new Vector3(-2.5f, 0.9f, 1.5f), Quaternion.Euler(350, 250, 0)),
            ];

            public PrisonerProps(GameObject gO)
            {
                GameObject crateObj = gO.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_Crate_Sealed (1)");
                GameObject emptyBoardObj = gO.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_BoardGame");
                GameObject playerChairObj = gO.FindChild("Props_PrisonCell/LowerCell/Prefab_IP_DW_Chair");
                GameObject prisonerChairObj = gO.FindChild("Interactibles_PrisonCell/PrisonerSequence/Prefab_IP_DW_Chair");

                GameObject torchObj = Instantiate(FindObjectOfType<VisionTorchSocket>().gameObject, playerChairObj.transform);
                torchObj.transform.localPosition = new Vector3(-0.9f, 0.9f, 0);
                torchObj.transform.localRotation = Quaternion.Euler(5, 20, 350);

                crate = new Prop(crateObj, s_movedPoses[0]);
                emptyBoard = new Prop(emptyBoardObj, s_movedPoses[1]);
                playerChair = new Prop(playerChairObj, s_movedPoses[2]);
                prisonerChair = new Prop(prisonerChairObj, s_movedPoses[3]);
                torchSocket = new Prop(torchObj, s_movedPoses[4]);
            }

            public Prop[] GetProps()
            {
                return [crate, emptyBoard, playerChair, prisonerChair, torchSocket];
            }
        }

        private PrisonerProps _props;
        private DreamLanternController _prisonerLantern, _lanternCopy;
        private OWLight _torchSpotlight;
        private Transform _elevatorPos, _seatPos, _cueMarker;
        private string _talkToText, _giveTorchText;
        private float _chairCueZ = 5.2f, _elevatorCueZ = -9.2f;
        private float _initTorchPlaceTime, _torchSpotlightDelay, _initWalkTime, _eyesCloseTime, _initFinalWordsTime;
        private bool _spotlightingTorch, _eyesClosed;

        private void Start()
        {
            PrisonerDirector = FindObjectOfType<PrisonerDirector>();
            PrisonerDialogue = PrisonerDirector._characterDialogueTree;

            Pose prisonerChessPose = new Pose { position = new Vector3(4, -35.105f, 0.2f), rotation = Quaternion.Euler(0, 270, 0) };
            ChessGame = InhabitantChess.Instance.InstantiateChessGame(transform, prisonerChessPose);
            ChessGame.gameObject.SetActive(false);

            _props = new PrisonerProps(gameObject);
            SetTorchSocket(_props.torchSocket.gameObject.GetComponent<VisionTorchSocket>());
            // make a copy of the prisoner's lantern to be used as a lighting prop
            GameObject lantern = PrisonerDirector._prisonerController._lantern.gameObject;
            GameObject lanternClone = Instantiate(lantern);
            lanternClone.transform.SetParent(transform.Find("Props_PrisonCell/LowerCell"));
            lanternClone.transform.localPosition = new Vector3(6.075f, 0.96f, 0.35f);
            lanternClone.transform.localRotation = Quaternion.Euler(5, 260, 0);
            lanternClone.SetActive(false);
            _prisonerLantern = lantern.GetComponent<DreamLanternController>();
            _lanternCopy = lanternClone.GetComponent<DreamLanternController>();
            _lanternCopy._light.transform.localPosition = new Vector3(0, 1, 0);
            _lanternCopy.SetLit(true);
            _lanternCopy.SetFocus(1);

            _cueMarker = PrisonerDirector._torchReturnCueMarker;
            _seatPos = new GameObject().transform;
            _seatPos.localRotation = Quaternion.identity;
            _seatPos.localPosition = new Vector3(3.9f, -0.0001f, 0.9f);
            _elevatorPos = new GameObject().transform;
            _elevatorPos.localPosition = new Vector3(-0.0377f, 0, 6.5023f);
            _elevatorPos.localRotation = Quaternion.Euler(0, 182.6558f, 0);

            PrisonerDirector._prisonerBrain.OnArriveAtElevatorDoor += OnArriveAtElevator;
            PrisonerDirector._prisonerEffects.OnReadyToReceiveTorch += OnReadyForTorch;
            TextTranslation.Get().OnLanguageChanged += () => Translations.UpdateCharacterDialogue(PrisonerDialogue);
            TextTranslation.Get().OnLanguageChanged += UpdatePromptText;
            enabled = false;
        }

        public void SetText(TextAsset text)
        {
            DialogueText = text;
        }

        private void SetTorchSocket(VisionTorchSocket socket)
        {
            TorchSocket = socket;
            TorchSocket.OnSocketablePlaced = (OWItemSocket.SocketEvent)Delegate.Combine(TorchSocket.OnSocketablePlaced, new OWItemSocket.SocketEvent(OnPlayerPlaceTorch));
            TorchSocket.EnableInteraction(false);

            _torchSpotlight = TorchSocket.gameObject.AddComponent<OWLight>();
            _torchSpotlight.SetRange(2);
            _torchSpotlight.SetIntensity(0);
        }

        public void UpdatePromptText()
        {
            _talkToText = UITextLibrary.GetString(UITextType.TalkToPrompt) + " " +
                          TextTranslation.Translate(PrisonerDialogue._characterName);
            _giveTorchText = UITextLibrary.GetString(UITextType.GivePrompt) + " " +
                             UITextLibrary.GetString(UITextType.ItemVisionTorchPrompt) + "?";
        }

        public void OnArriveAtElevator()
        {
            if (!CanTriggerSequence) return;

            // turn off our addition since we'll use MoveToElevatorDoor for moving 'em around later
            PrisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= OnArriveAtElevator;
            // original sequence assumes only dialogue is after emerge, just swap the two listeners here
            PrisonerDialogue.OnEndConversation -= PrisonerDirector.OnFinishDialogue;
            PrisonerDialogue.OnEndConversation += OnFinishElevatorDialogue;

            PrisonerDialogue.SetTextXml(DialogueText);
            Translations.UpdateCharacterDialogue(PrisonerDialogue);
            UpdatePromptText();
            PrisonerDialogue._interactVolume._screenPrompt.SetText(_giveTorchText);
        }

        public void OnReadyForTorch()
        {
            if (!CanTriggerSequence) return;

            PrisonerDirector._prisonerEffects.OnReadyToReceiveTorch -= OnReadyForTorch;
            // stop prisoner from playing torch request animation after arriving at chair later
            PrisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= PrisonerDirector.OnPrisonerArriveAtElevatorDoor;

            EnableConversation();
            PrisonerDirector._waitingForPlayerToReturnTorch = false;
            PrisonerDirector._prisonerTorchSocket.EnableInteraction(false);
        }

        private void OnFinishElevatorDialogue()
        {
            PrisonerDialogue.OnEndConversation -= OnFinishElevatorDialogue;
            PrisonerDialogue.OnEndConversation += OnFinishGameDialogue;
            DisableConversation();
            // only behavior that uses the default animation explicitly - resetting them
            PrisonerDirector._prisonerBrain.BeginBehavior(PrisonerBehavior.WaitForProjection, 1f);
            TorchSocket.EnableInteraction(true);
            _torchSpotlightDelay = Time.time + 2f;
            _spotlightingTorch = true;
            enabled = true;
        }

        public void OnPlayerPlaceTorch(OWItem Item)
        {
            TorchSocket.OnSocketablePlaced = (OWItemSocket.SocketEvent)Delegate.Remove(TorchSocket.OnSocketablePlaced, new OWItemSocket.SocketEvent(OnPlayerPlaceTorch));
            TorchSocket.OnSocketableRemoved = (OWItemSocket.SocketEvent)Delegate.Combine(TorchSocket.OnSocketableRemoved, new OWItemSocket.SocketEvent(OnPlayerPickupTorch));

            TorchSocket.EnableInteraction(false);
            _torchSpotlight.SetIntensity(0);
            _cueMarker.localPosition = new Vector3(_cueMarker.localPosition.x, _cueMarker.localPosition.y, _chairCueZ);
            _initTorchPlaceTime = Time.time;
            _state = PrisonerState.ReactingToTorch;
        }

        public void OnFinishGameDialogue()
        {
            bool readyToLeave = DialogueConditionManager.SharedInstance.GetConditionState("IC_READY_TO_LEAVE");
            if (readyToLeave)
            {
                DisableConversation();
                PrisonerDialogue.OnEndConversation -= OnFinishGameDialogue;
                _cueMarker.localPosition = new Vector3(_cueMarker.localPosition.x, _cueMarker.localPosition.y, _elevatorCueZ);
                _initFinalWordsTime = Time.time;
                _state = PrisonerState.SaidFinalWords;
            }
            //else Logger.Log("Talked to prisoner and decided to continue playing");
        }

        public void OnPlayerPickupTorch(OWItem Item)
        {
            TorchSocket.OnSocketableRemoved = (OWItemSocket.SocketEvent)Delegate.Remove(TorchSocket.OnSocketableRemoved, new OWItemSocket.SocketEvent(OnPlayerPickupTorch));
            TorchSocket.EnableInteraction(false);
        }

        private void Update()
        {
            float t = Time.time;
            if (_spotlightingTorch && t >= _torchSpotlightDelay)
            {
                _spotlightingTorch = false;
                _torchSpotlight.SetIntensity(1);
                OnSpotlightTorch?.Invoke();
            }
            UpdatePrisonerSequence(t);
        }

        private void UpdatePrisonerSequence(float t)
        {
            // roughly control flow of sequence through state + timers
            if (_state == PrisonerState.ReactingToTorch && t >= _initTorchPlaceTime + 2f)
            {
                // delay reaction enough for player to turn around
                OnPrisonerCurious?.Invoke();
                PrisonerDirector._prisonerBrain.BeginBehavior(PrisonerBehavior.MoveToElevatorDoor, _cueMarker, 5f);
                // TODO: disable movement blocker so player can't grief them?
                _initWalkTime = t;
                _state = PrisonerState.WalkingToMarker;
            }
            if (_state == PrisonerState.WalkingToMarker && t >= _initWalkTime + 10f)
            {
                _state = PrisonerState.BeingSeated;
                SleepPlayer();
            }
            if (_state == PrisonerState.SaidFinalWords && t >= _initFinalWordsTime + 2f)
            {
                _state = PrisonerState.BeingRestored;
                SleepPlayer();
            }
            if (_eyesClosed)
            {
                if (_state == PrisonerState.BeingSeated && t >= _eyesCloseTime + 4f)
                {
                    _state = PrisonerState.Seated;
                    SetUpGame();
                }
                else if (_state == PrisonerState.BeingRestored && t >= _eyesCloseTime + 4f)
                {
                    _state = PrisonerState.BackAtElevator;
                    CleanUpGame();
                }
                if (t >= _eyesCloseTime + 10f)
                {
                    WakePlayer();
                }
            }
        }

        public void SetUpGame()
        {
            SeatPrisoner(_seatPos);
            MoveProps();
            PrisonerDialogue._interactVolume._screenPrompt.SetText(_talkToText);
            EnableConversation();
            SetPlayerChairCollision(false);
            ChessGame.gameObject.SetActive(true);
            OnSetupGame?.Invoke();
        }

        public void CleanUpGame()
        {
            RestorePrisoner(_elevatorPos);
            ResetProps();
            DisableConversation();
            SetPlayerChairCollision(true);
            TorchSocket.EnableInteraction(true);
            // VisionTorchItem is disabled by director at start, and only enabled by the vision-sharing sequence
            // I put it down here - might as well avoid affecting other stuff earlier
            TorchSocket._socketedItem.EnableInteraction(true);
            OnCleanupGame?.Invoke();
        }

        private void SeatPrisoner(Transform seatPos)
        {
            // animator sitting state: 263823602 "WaitingForPlayer"
            // since we're not using the triggers for transitioning states, should be fine to
            // call BeginBehavior(PrisonerBehavior.WaitForTorchReturn, ..)
            int sittingState = 263823602;
            PrisonerDirector._prisonerBrain._controller.StopMoving();
            PrisonerDirector._prisonerBrain._controller.StopFacing();
            PrisonerDirector._prisonerBrain.transform.localPosition = seatPos.localPosition;
            PrisonerDirector._prisonerBrain.transform.localRotation = seatPos.localRotation;
            // they sit behind the conversation trigger :(
            PrisonerDialogue._interactVolume.transform.SetLocalPositionZ(-3);
            PrisonerDirector._prisonerEffects._animator.Play(sittingState);
        }

        private void RestorePrisoner(Transform elevatorPos)
        {
            PrisonerDirector._prisonerBrain.transform.localPosition = elevatorPos.localPosition;
            PrisonerDirector._prisonerBrain.transform.localRotation = elevatorPos.localRotation;
            PrisonerDialogue._interactVolume.transform.SetLocalPositionZ(0);
            PrisonerDirector._prisonerBrain.BeginBehavior(PrisonerBehavior.WaitForTorchReturn, _cueMarker, 0f);
        }

        private void SleepPlayer()
        {
            OWInput.ChangeInputMode(InputMode.None);
            Locator.GetPromptManager().SetPromptsVisible(false);
            Locator.GetPlayerCamera().GetComponent<PlayerCameraEffectController>().CloseEyes(3f);
            _eyesCloseTime = Time.time + 3f;
            _eyesClosed = true;
        }

        private void WakePlayer()
        {
            OWInput.ChangeInputMode(InputMode.Character);
            Locator.GetPromptManager().SetPromptsVisible(true);
            Locator.GetPlayerCamera().GetComponent<PlayerCameraEffectController>().OpenEyes(1f);
            _eyesClosed = false;
        }

        private void MoveProps()
        {
            foreach (Prop prop in _props.GetProps())
                prop.Move();

            // make board invisible so we can use the collision
            foreach (MeshRenderer mesh in _props.emptyBoard.gameObject.GetComponentsInChildren<MeshRenderer>())
                mesh.enabled = false;
            _prisonerLantern.gameObject.SetActive(false);
            _lanternCopy.gameObject.SetActive(true);
            _lanternCopy.enabled = true;
            StartCoroutine(LanternLightHack());
        }

        // need "open" state of lantern with unfocused beam - both controlled by DreamLantern's UpdateVisuals
        public IEnumerator LanternLightHack()
        {
            yield return new WaitUntil(() => _lanternCopy.GetLight().range == 30);
            _lanternCopy._light.SetIntensityScale(1.5f);
            _lanternCopy._light.range = 4;
            _lanternCopy._light.GetLight().spotAngle = 130;
        }

        private void ResetProps()
        {
            foreach (Prop prop in _props.GetProps())
                prop.Reset();

            foreach (MeshRenderer mesh in _props.emptyBoard.gameObject.GetComponentsInChildren<MeshRenderer>())
                mesh.enabled = true;
            _prisonerLantern.gameObject.SetActive(true);
            _lanternCopy.gameObject.SetActive(false);
            _lanternCopy.enabled = false;
        }

        public void SetPlayerChairCollision(bool enabled)
        {
            _props.playerChair.gameObject.GetComponent<MeshCollider>().enabled = enabled;
        }

        public void EnableConversation()
        {
            PrisonerDialogue._interactVolume.EnableInteraction();
        }

        public void DisableConversation()
        {
            PrisonerDialogue._interactVolume.DisableInteraction();
        }

        public void OnFurnitureCleanupFinished()
        {
            // delayed after event fired from CleanUpGame
            ChessGame.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            PrisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= OnArriveAtElevator;
            PrisonerDirector._prisonerEffects.OnReadyToReceiveTorch -= OnReadyForTorch;
            TextTranslation.Get().OnLanguageChanged -= () => Translations.UpdateCharacterDialogue(PrisonerDialogue);
            TextTranslation.Get().OnLanguageChanged -= UpdatePromptText;
            PrisonerDialogue.OnEndConversation -= OnFinishElevatorDialogue;
            PrisonerDialogue.OnEndConversation -= OnFinishGameDialogue;
            TorchSocket.OnSocketablePlaced = (OWItemSocket.SocketEvent)Delegate.Remove(TorchSocket.OnSocketablePlaced, new OWItemSocket.SocketEvent(OnPlayerPlaceTorch));
            TorchSocket.OnSocketableRemoved = (OWItemSocket.SocketEvent)Delegate.Remove(TorchSocket.OnSocketableRemoved, new OWItemSocket.SocketEvent(OnPlayerPickupTorch));
        }
    }
}
