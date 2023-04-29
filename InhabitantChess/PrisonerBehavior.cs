using InhabitantChess.Util;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    public class PrisonerBehavior : MonoBehaviour
    {
        private PrisonerDirector _prisonerDirector;
        private CharacterDialogueTree _prisonerDialogue;
        private TextAsset _dialogue;

        private void Start()
        {
            _prisonerDirector = FindObjectOfType<PrisonerDirector>();
            _prisonerDialogue = _prisonerDirector._characterDialogueTree;
            _prisonerDirector._prisonerBrain.OnArriveAtElevatorDoor += UpdateDialogueText;
            _prisonerDirector._prisonerEffects.OnReadyToReceiveTorch += UpdateDialogueNode;
        }

        public void SetText(TextAsset text)
        {
            _dialogue = text;
        }

        private void UpdateDialogueText()
        {
            _prisonerDialogue.SetTextXml(_dialogue);
            Translations.UpdateCharacterDialogue(_prisonerDialogue);
        }

        private void UpdateDialogueNode()
        {
            // stop director from looping back to start (prisoner lights lamp etc.)
            _prisonerDialogue.OnEndConversation -= _prisonerDirector.OnFinishDialogue;

            _prisonerDialogue._interactVolume?.EnableInteraction();
            _prisonerDirector._prisonerTorchSocket.EnableInteraction(false);
        }

        public void EnableConversation()
        {
            _prisonerDialogue._interactVolume?.EnableInteraction();
        }

        public void DisableConversation()
        {
            _prisonerDialogue._interactVolume?.DisableInteraction();
        }

        private void OnDestroy()
        {
            _prisonerDirector._prisonerBrain.OnArriveAtElevatorDoor -= UpdateDialogueText;
            _prisonerDirector._prisonerEffects.OnReadyToReceiveTorch -= UpdateDialogueNode;
        }
    }
}
