using InhabitantChess.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    public class PrisonerSequence : MonoBehaviour
    {
        private PrisonerDirector _prisonerDirector;
        private CharacterDialogueTree _prisonerDialogue;
        private TextAsset _dialogue;
        private VisionTorchSocket _torchSocket;
        private OWLight _torchSpotlight;
        private DreamLanternController _prisonerLantern;
        private Dictionary<string, GameObject> _props;
        private List<(string name, Vector3 pos, Vector3 rot)> _ogTransforms, _movedTransforms;
        private Transform _elevatorPos, _seatPos, _cueMarker;
        private float _chairCueZ = 5.2f, _elevatorCueZ = -9.2f;
        private float _initTorchPlaceTime, _initPrisonerWalkTime, _eyesCloseTime;
        private bool _prisonerReactingToTorch, _prisonerWalkingToMarker, _prisonerSeated, _eyesClosed;

        private void Start()
        {
            GameObject prisonCell = InhabitantChess.Instance.PrisonCell;
            GameObject prisonerRig = prisonCell.FindChild("Ghosts_PrisonCell/GhostNodeMap_PrisonCell_Lower/Prefab_IP_GhostBird_Prisoner/Ghostbird_IP_ANIM");
            GameObject lantern = Instantiate(prisonerRig.FindChild("Ghostbird_Skin_01:Ghostbird_Rig_V01:Base/Ghostbird_Skin_01:Ghostbird_Rig_V01:Root/Ghostbird_Skin_01:Ghostbird_Rig_V01:Spine01/Ghostbird_Skin_01:Ghostbird_Rig_V01:Spine02/Ghostbird_Skin_01:Ghostbird_Rig_V01:Spine03/Ghostbird_Skin_01:Ghostbird_Rig_V01:Spine04/Ghostbird_Skin_01:Ghostbird_Rig_V01:ClavicleR/Ghostbird_Skin_01:Ghostbird_Rig_V01:ShoulderR/Ghostbird_Skin_01:Ghostbird_Rig_V01:ElbowR/Ghostbird_Skin_01:Ghostbird_Rig_V01:WristR/LanternCarrySocket/GhostLantern"));
            lantern.transform.SetParent(prisonCell.transform.Find("Props_PrisonCell/LowerCell"));
            lantern.transform.localPosition = new Vector3(6.075f, 0.96f, 0.35f);
            lantern.transform.localRotation = Quaternion.Euler(5, 260, 0);
            lantern.SetActive(false);
            _prisonerLantern = lantern.GetComponent<DreamLanternController>();

            _props = new Dictionary<string, GameObject>
            {
                { "crate", prisonCell.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_Crate_Sealed (1)") },
                { "uselessBoard", prisonCell.FindChild("Props_PrisonCell/LowerCell/Props_IP_DW_BoardGame") },
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
                Vector3 pos = new Vector3(prop.transform.localPosition.x , prop.transform.localPosition.y , prop.transform.localPosition.z );
                Vector3 rot = new Vector3(prop.transform.localRotation.x, prop.transform.localRotation.y, prop.transform.localRotation.z );
                _ogTransforms.Add((p.Key, pos, rot));
            }
            _movedTransforms.Add((_props.ElementAt(0).Key, new Vector3(4, 0.035f, 0), new Vector3(0, 180, 0)));
            _movedTransforms.Add((_props.ElementAt(1).Key, new Vector3(4, 0.895f, 0), new Vector3(0, 270, 0)));
            _movedTransforms.Add((_props.ElementAt(2).Key, new Vector3(4, 0.035f, 1.75f), new Vector3(0, 180, 0)));
            _movedTransforms.Add((_props.ElementAt(3).Key, new Vector3(-0.75f, 0, 3.9f), new Vector3(0, 261.3473f, 0)));
            _movedTransforms.Add((_props.ElementAt(4).Key, new Vector3(-2.5f, 0.9f, 1.5f), new Vector3(350, 250, 0)));

            // testing - delete later
            GameObject torch = Instantiate(GameObject.Find("Prefab_IP_VisionTorchProjector"), torchSocket.transform);
            torch.transform.localPosition = Vector3.zero;
            torch.transform.localRotation = Quaternion.identity;

            _prisonerDirector = FindObjectOfType<PrisonerDirector>();
            _prisonerDialogue = _prisonerDirector._characterDialogueTree;
            _cueMarker = _prisonerDirector._torchReturnCueMarker;
            _seatPos = new GameObject().transform;
            _seatPos.localRotation = Quaternion.identity;
            _seatPos.localPosition = new Vector3(3.9f, -0.0001f, 0.9f);

            _prisonerDirector._prisonerBrain.OnArriveAtElevatorDoor += OnArriveAtElevator;
            _prisonerDirector._prisonerEffects.OnReadyToReceiveTorch += OnReadyForTorch;
            enabled = false;
        }

        public void SetText(TextAsset text)
        {
            _dialogue = text;
        }

        private void SetTorchSocket(VisionTorchSocket socket)
        {
            // TOOD: Patch "Give" prompt into a "Place" in FirstPersonManipulator? seems hardcoded to be give/take
            _torchSocket = socket;
            _torchSocket.OnSocketablePlaced = (OWItemSocket.SocketEvent)Delegate.Combine(_torchSocket.OnSocketablePlaced, new OWItemSocket.SocketEvent(OnPlayerPlaceTorch));
            _torchSocket.EnableInteraction(false);

            _torchSpotlight = _torchSocket.gameObject.AddComponent<OWLight>();
            _torchSpotlight.SetRange(2);
            _torchSpotlight.SetIntensity(0);
        }

        private void OnArriveAtElevator()
        {
            // turn off our addition since we'll use MoveToElevatorDoor for moving 'em around later
            _prisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= OnArriveAtElevator;
            // original sequence assumes only dialogue is after emerge, just swap the two listeners here
            _prisonerDialogue.OnEndConversation -= _prisonerDirector.OnFinishDialogue;
            _prisonerDialogue.OnEndConversation += OnFinishElevatorDialogue;

            _prisonerDialogue.SetTextXml(_dialogue);
            Translations.UpdateCharacterDialogue(_prisonerDialogue);

            // ItemTool.UpdateState controls the "Give Vision Torch" interact text
            // so I have to do it myself :(
            string givePrompt = UITextLibrary.GetString(UITextType.GivePrompt) + " " + 
                                UITextLibrary.GetString(UITextType.ItemVisionTorchPrompt) + "?";
            _prisonerDialogue._interactVolume._screenPrompt.SetText(givePrompt);

            _elevatorPos = _prisonerDirector._prisonerBrain.transform;
        }

        private void OnReadyForTorch()
        {
            // stop prisoner from playing torch request animation after arriving at chair later
            _prisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= _prisonerDirector.OnPrisonerArriveAtElevatorDoor;

            EnableConversation();
            _prisonerDirector._waitingForPlayerToReturnTorch = false;
            _prisonerDirector._prisonerTorchSocket.EnableInteraction(false);
        }

        private void OnFinishElevatorDialogue()
        {
            DisableConversation();
            // only behavior that uses the default animation explicitly - resetting them
            _prisonerDirector._prisonerBrain.BeginBehavior(PrisonerBehavior.WaitForProjection, 1f);
            _torchSocket.EnableInteraction(true);
            _torchSpotlight.SetIntensity(1);
            enabled = true;
        }

        private void OnPlayerPlaceTorch(OWItem Item)
        {
            _torchSocket.EnableInteraction(false);
            _torchSpotlight.SetIntensity(0);
            _cueMarker.localPosition = new Vector3(_cueMarker.localPosition.x, _cueMarker.localPosition.y, _chairCueZ);
            _initTorchPlaceTime = Time.time;
            _prisonerReactingToTorch = true;
        }

        private void Update()
        {
            if (_prisonerReactingToTorch && Time.time >= _initTorchPlaceTime + 2f)
            {
                // delay reaction enough for player to turn around
                _prisonerDirector._prisonerEffects.PlayVoiceAudioNear(AudioType.Ghost_Identify_Curious);
                _prisonerDirector._prisonerBrain.BeginBehavior(PrisonerBehavior.MoveToElevatorDoor, _cueMarker, 5f);
                _prisonerReactingToTorch = false;
                _initPrisonerWalkTime = Time.time;
                _prisonerWalkingToMarker = true;
            }
            if (_prisonerWalkingToMarker && Time.time >= _initPrisonerWalkTime + 10f)
            {
                Locator.GetPlayerController().LockMovement();
                Locator.GetPlayerCamera().GetComponent<PlayerCameraEffectController>().CloseEyes(3f);
                _prisonerWalkingToMarker = false;
                _eyesCloseTime = Time.time + 3f;
                _eyesClosed = true;
            }
            if (_eyesClosed)
            {
                if (!_prisonerSeated && Time.time >= _eyesCloseTime + 6f)
                {
                    SeatPrisoner(_seatPos);
                    MoveProps();
                    _prisonerSeated = true;
                }
                if (Time.time >= _eyesCloseTime + 10f)
                {
                    Locator.GetPlayerController().UnlockMovement();
                    Locator.GetPlayerCamera().GetComponent<PlayerCameraEffectController>().OpenEyes(1f);
                    _eyesClosed = false;

                }
            }
        }

        private void SeatPrisoner(Transform seatPos)
        {
            // animator sitting state: 263823602 "WaitingForPlayer"
            // since we're not using the triggers for transitioning states, should be fine to
            // call BeginBehavior(PrisonerBehavior.WaitForTorchReturn, ..)
            int sittingState = 263823602;
            _prisonerDirector._prisonerBrain._controller.StopMoving();
            _prisonerDirector._prisonerBrain.transform.localPosition = seatPos.localPosition;
            _prisonerDirector._prisonerBrain.transform.localRotation = seatPos.localRotation;
            _prisonerDirector._prisonerEffects._animator.Play(sittingState);
        }

        private void MoveProps()
        {
            foreach (KeyValuePair<string, GameObject> p in _props)
            {
                var t = _movedTransforms.Where(t => t.name.Equals(p.Key)).FirstOrDefault();
                p.Value.transform.localPosition = t.pos;
                p.Value.transform.localRotation = Quaternion.Euler(t.rot);
            }
            _prisonerLantern.gameObject.SetActive(true);
        }

        private void ResetProps()
        {
            foreach (KeyValuePair<string, GameObject> p in _props)
            {
                var t = _ogTransforms.Where(t => t.name.Equals(p.Key)).FirstOrDefault();
                p.Value.transform.localPosition = t.pos;
                p.Value.transform.localRotation = Quaternion.Euler(t.rot);
            }
            _prisonerLantern.gameObject.SetActive(false);
        }

        private void EnableConversation()
        {
            _prisonerDialogue._interactVolume?.EnableInteraction();
        }

        private void DisableConversation()
        {
            _prisonerDialogue._interactVolume?.DisableInteraction();
        }

        private void OnDestroy()
        {
            _prisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= OnArriveAtElevator;
            _prisonerDirector._prisonerEffects.OnReadyToReceiveTorch -= OnReadyForTorch;
            _prisonerDialogue.OnEndConversation -= OnFinishElevatorDialogue;
            _torchSocket.OnSocketablePlaced = (OWItemSocket.SocketEvent)Delegate.Remove(_torchSocket.OnSocketablePlaced, new OWItemSocket.SocketEvent(OnPlayerPlaceTorch));
        }
    }
}
