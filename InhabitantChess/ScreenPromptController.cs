using InhabitantChess.Util;
using System.Collections.Generic;
using UnityEngine;

namespace InhabitantChess
{
    public class ScreenPromptController : MonoBehaviour
    {
        public static ScreenPromptController Instance { get; private set; }

        private Dictionary<PromptType, ScreenPrompt> _prompts;
        private Dictionary<PromptType, bool> _activePrompts;

        public enum PromptType
        {
            Score,
            BoardMove,
            Overhead,
            Lean,
            SpawnChessGame
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_prompts == null)
            {
                _prompts = new Dictionary<PromptType, ScreenPrompt>
                {
                    { PromptType.Score, new ScreenPrompt(Translations.GetTranslation("IC_SCORE")) },
                    { PromptType.BoardMove, MakeScreenPrompt(Controls.BoardMove, Translations.GetTranslation("IC_BOARDMOVE")) },
                    { PromptType.Overhead, MakeScreenPrompt(Controls.Overhead, Translations.GetTranslation("IC_OVERHEAD")) },
                    { PromptType.Lean, MakeScreenPrompt(Controls.PanCamera, Translations.GetTranslation("IC_LEAN")) },
                    { PromptType.SpawnChessGame, MakeScreenPrompt(Controls.SpawnGame, Translations.GetTranslation("IC_SPAWNGAME")) }
                };
                _activePrompts = new();
            }

            PromptManager pm = Locator.GetPromptManager();
            foreach ((_, var screenPrompt) in _prompts)
                pm.AddScreenPrompt(screenPrompt, PromptPosition.UpperRight);
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
                if (Locator.GetPromptManager() != null)
                    Locator.GetPromptManager().RemoveScreenPrompt(prompt.Value, PromptPosition.UpperRight);
            }
        }

        public void SetPromptVisibility(PromptType type, bool visible)
        {
            _activePrompts[type] = visible;
        }

        public void SetScore(int playerWins, int playerLosses)
        {
            _prompts[PromptType.Score].SetText(Translations.GetTranslation("IC_SCORE") + $" {playerWins} - {playerLosses}");
        }

        private ScreenPrompt MakeScreenPrompt(IInputCommands cmd, string prompt)
        {
            return new ScreenPrompt(cmd, prompt + "<CMD>", 0, ScreenPrompt.DisplayState.Normal, false);
        }
    }
}
