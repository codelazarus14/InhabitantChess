using InhabitantChess.Util;
using System.Collections.Generic;
using UnityEngine;

namespace InhabitantChess
{
    public class ScreenPrompts : MonoBehaviour
    {
        private Dictionary<PromptType, ScreenPrompt> _prompts;
        private List<ScreenPrompt> _activePrompts;
        private PromptManager _promptManager;
        private bool _updateAfterPause;

        public enum PromptType
        {
            BoardMove,
            Overhead,
            LeanForward
        }

        private void Start()
        {
            if (_prompts == null)
            {
                _prompts = new Dictionary<PromptType, ScreenPrompt>
                {
                    { PromptType.BoardMove, MakeScreenPrompt(InputLibrary.interact, Translations.GetTranslation(Translations.ICText.BoardMove) + "<CMD>") },
                    { PromptType.Overhead, MakeScreenPrompt(InputLibrary.landingCamera, Translations.GetTranslation(Translations.ICText.Overhead) + "<CMD>") },
                    // TODO: fix to isolate W from the normal WASD command icon
                    { PromptType.LeanForward, MakeScreenPrompt(InputLibrary.moveXZ, Translations.GetTranslation(Translations.ICText.LeanForward) + "<CMD>") }
                };
                _activePrompts = new List<ScreenPrompt>();
            }

            PromptManager pm = Locator.GetPromptManager();
            pm.AddScreenPrompt(_prompts[PromptType.BoardMove], PromptPosition.UpperRight);
            pm.AddScreenPrompt(_prompts[PromptType.Overhead], PromptPosition.UpperRight);
            pm.AddScreenPrompt(_prompts[PromptType.LeanForward], PromptPosition.UpperRight);
        }

        private void Update()
        {
            if (OWTime.IsPaused() && !_updateAfterPause)
            {
                foreach (var prompt in _activePrompts) prompt.SetVisibility(false);
                _updateAfterPause = true;
            }
            else if (!OWTime.IsPaused() && _updateAfterPause)
            {
                foreach (var prompt in _activePrompts) prompt.SetVisibility(true);
                _updateAfterPause = false;
            }
        }

        private void OnDestroy()
        {
            foreach (var prompt in _prompts)
            {
                Locator.GetPromptManager()?.RemoveScreenPrompt(prompt.Value, PromptPosition.UpperRight);
            }
        }

        public void SetPromptVisibility(PromptType type, bool visible)
        {
            _prompts[type].SetVisibility(visible);
            if (visible && _prompts[type].IsVisible()) _activePrompts.Add(_prompts[type]);
            else if (!visible && _prompts[type].IsVisible()) _activePrompts.Remove(_prompts[type]);
        }

        private ScreenPrompt MakeScreenPrompt(IInputCommands cmd, string prompt)
        {
            return new ScreenPrompt(cmd, prompt, 0, ScreenPrompt.DisplayState.Normal, false);
        }


    }
}
