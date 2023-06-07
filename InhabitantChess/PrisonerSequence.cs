using InhabitantChess.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    public class PrisonerSequence : MonoBehaviour
    {
        public PrisonerDirector PrisonerDirector { get; private set; }
        public CharacterDialogueTree PrisonerDialogue { get; private set; }
        public TextAsset DialogueText { get; private set; }
        public VisionTorchSocket TorchSocket { get; private set; }

        public bool CanTriggerSequence;

        public delegate void PrisonerSeqAudioEvent();
        public PrisonerSeqAudioEvent OnSpotlightTorch;
        public PrisonerSeqAudioEvent OnPrisonerCurious;
        public PrisonerSeqAudioEvent OnSetupGame;
        public PrisonerSeqAudioEvent OnCleanupGame;

        private OWLight _torchSpotlight;
        private DreamLanternController _prisonerLantern, _lanternCopy;
        private Dictionary<string, GameObject> _props;
        private List<(string name, Vector3 pos, Quaternion rot)> _ogTransforms, _movedTransforms;
        private Transform _elevatorPos, _seatPos, _cueMarker;
        private string _talkToText, _giveTorchText;
        private float _chairCueZ = 5.2f, _elevatorCueZ = -9.2f;
        private float _initTorchPlaceTime, _torchSpotlightDelay, _initWalkTime, _eyesCloseTime, _initFinalWordsTime;
        private bool _spotlightingTorch, _eyesClosed;
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

        private void Start()
        {
            PrisonerDirector = FindObjectOfType<PrisonerDirector>();
            PrisonerDialogue = PrisonerDirector._characterDialogueTree;

            GameObject prisonCell = InhabitantChess.Instance.PrisonCell;
            // make a copy of the prisoner's lantern to be used as a lighting prop
            GameObject lantern = PrisonerDirector._prisonerController._lantern.gameObject;
            GameObject lanternClone = Instantiate(lantern);
            lanternClone.transform.SetParent(prisonCell.transform.Find("Props_PrisonCell/LowerCell"));
            lanternClone.transform.localPosition = new Vector3(6.075f, 0.96f, 0.35f);
            lanternClone.transform.localRotation = Quaternion.Euler(5, 260, 0);
            lanternClone.SetActive(false);
            _prisonerLantern = lantern.GetComponent<DreamLanternController>();
            _lanternCopy = lanternClone.GetComponent<DreamLanternController>();
            _lanternCopy._light.transform.localPosition = new Vector3(0, 1, 0);
            _lanternCopy.SetLit(true);
            _lanternCopy.SetFocus(1);

            _props = new Dictionary<string, GameObject>
            {
                { "crate", prisonCell.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_Crate_Sealed (1)") },
                { "emptyBoard", prisonCell.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_BoardGame") },
                { "playerChair", prisonCell.FindChild("Props_PrisonCell/LowerCell/Prefab_IP_DW_Chair") },
                { "prisonerChair", prisonCell.FindChild("Interactibles_PrisonCell/PrisonerSequence/Prefab_IP_DW_Chair") }
            };
            GameObject torchSocket = Instantiate(FindObjectOfType<VisionTorchSocket>().gameObject, _props["playerChair"].transform);
            torchSocket.transform.localPosition = new Vector3(-0.9f, 0.9f, 0);
            torchSocket.transform.localRotation = Quaternion.Euler(5, 20, 350);
            SetTorchSocket(torchSocket.GetComponent<VisionTorchSocket>());
            _props.Add("torchSocket", torchSocket);

            _ogTransforms = new();
            _movedTransforms = new();
            foreach (var p in _props)
            {
                GameObject prop = p.Value;
                Transform pTrans = prop.transform;
                Vector3 pos = new Vector3(pTrans.localPosition.x, pTrans.localPosition.y, pTrans.localPosition.z);
                Quaternion rot = new Quaternion(pTrans.localRotation.x, pTrans.localRotation.y, pTrans.localRotation.z, pTrans.localRotation.w);
                _ogTransforms.Add((p.Key, pos, rot));
            }
            _movedTransforms.Add((_props.ElementAt(0).Key, new Vector3(4, 0.035f, 0), Quaternion.Euler(0, 180, 0)));
            _movedTransforms.Add((_props.ElementAt(1).Key, new Vector3(4, 0.85f, 0.2f), Quaternion.Euler(0, 270, 0)));
            _movedTransforms.Add((_props.ElementAt(2).Key, new Vector3(4, 0.035f, 1.75f), Quaternion.Euler(0, 180, 0)));
            _movedTransforms.Add((_props.ElementAt(3).Key, new Vector3(-0.75f, 0, 3.9f), Quaternion.Euler(0, 261.3473f, 0)));
            _movedTransforms.Add((_props.ElementAt(4).Key, new Vector3(-2.5f, 0.9f, 1.5f), Quaternion.Euler(350, 250, 0)));

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
            // TOOD: Patch "Give" prompt into a "Place" in FirstPersonManipulator? seems hardcoded to be give/take
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
                PrisonerDialogue.OnEndConversation -= OnFinishGameDialogue;
                _cueMarker.localPosition = new Vector3(_cueMarker.localPosition.x, _cueMarker.localPosition.y, _elevatorCueZ);
                _initFinalWordsTime = Time.time;
                _state = PrisonerState.SaidFinalWords;
            }
            //else Logger.Log("Talked to prisoner and decided to continue playing");
        }

        public void OnPlayerPickupTorch(OWItem Item)
        {
            // TODO: why is this not disabling the torch socket after giving it back at the end
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

        public void SetUpGame(bool withAudio = true)
        {
            SeatPrisoner(_seatPos);
            MoveProps();
            PrisonerDialogue._interactVolume._screenPrompt.SetText(_talkToText);
            EnableConversation();
            SetPlayerChairCollision(false);
            InhabitantChess.Instance.BoardGame.SetActive(true);
            if (withAudio)
                OnSetupGame?.Invoke();
        }

        public void CleanUpGame()
        {
            RestorePrisoner(_elevatorPos);
            ResetProps();
            DisableConversation();
            SetPlayerChairCollision(true);
            TorchSocket.EnableInteraction(true);
            InhabitantChess.Instance.BoardGame.SetActive(false);
            InhabitantChess.Instance.ShortcutUnlocked();
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
            foreach (KeyValuePair<string, GameObject> p in _props)
            {
                var t = _movedTransforms.Where(t => t.name.Equals(p.Key)).FirstOrDefault();
                p.Value.transform.localPosition = t.pos;
                p.Value.transform.localRotation = t.rot;
            }
            // make board invisible so we can use the collision
            foreach (MeshRenderer mesh in _props["emptyBoard"].GetComponentsInChildren<MeshRenderer>())
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
            foreach (KeyValuePair<string, GameObject> p in _props)
            {
                var t = _ogTransforms.Where(t => t.name.Equals(p.Key)).FirstOrDefault();
                p.Value.transform.localPosition = t.pos;
                p.Value.transform.localRotation = t.rot;
            }
            foreach (MeshRenderer mesh in _props["emptyBoard"].GetComponentsInChildren<MeshRenderer>())
                mesh.enabled = true;
            _prisonerLantern.gameObject.SetActive(true);
            _lanternCopy.gameObject.SetActive(false);
            _lanternCopy.enabled = false;
        }

        public void SetPlayerChairCollision(bool enabled)
        {
            _props["playerChair"].GetComponent<MeshCollider>().enabled = enabled;
        }
        public void EnableConversation()
        {
            PrisonerDialogue._interactVolume.EnableInteraction();
        }

        public void DisableConversation()
        {
            PrisonerDialogue._interactVolume.DisableInteraction();
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
