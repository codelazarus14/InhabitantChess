using InhabitantChess.Util;
using System.Collections.Generic;
using UnityEngine;

namespace InhabitantChess
{
    public class ScreenPrompts : MonoBehaviour
    {
        private Dictionary<PromptType, ScreenPrompt> _prompts;
        private Dictionary<PromptType, bool> _activePrompts;

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
                    { PromptType.BoardMove, MakeScreenPrompt(InputLibrary.interact, Translations.GetTranslation("IC_BOARDMOVE") + "<CMD>") },
                    { PromptType.Overhead, MakeScreenPrompt(InputLibrary.landingCamera, Translations.GetTranslation("IC_OVERHEAD") + "<CMD>") },
                    // TODO: fix to isolate W from the normal WASD command icon
                    { PromptType.LeanForward, MakeScreenPrompt(InputLibrary.moveXZ, Translations.GetTranslation("IC_LEANFORWARD") + "<CMD>") }
                };
                _activePrompts = new();
            }

            PromptManager pm = Locator.GetPromptManager();
            pm.AddScreenPrompt(_prompts[PromptType.BoardMove], PromptPosition.UpperRight);
            pm.AddScreenPrompt(_prompts[PromptType.Overhead], PromptPosition.UpperRight);
            pm.AddScreenPrompt(_prompts[PromptType.LeanForward], PromptPosition.UpperRight);
        }

        private void Update()
        {
            foreach (PromptType t in _activePrompts.Keys)
            {
                _prompts[t].SetVisibility(_activePrompts[t] && !OWTime.IsPaused());
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
            _activePrompts[type] = visible;
        }

        private ScreenPrompt MakeScreenPrompt(IInputCommands cmd, string prompt)
        {
            return new ScreenPrompt(cmd, prompt, 0, ScreenPrompt.DisplayState.Normal, false);
        }
    }
}
