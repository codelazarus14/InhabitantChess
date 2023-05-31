using System;
using System.Collections.Generic;
using System.Linq;

namespace InhabitantChess.Util
{
    // borrowed from NH TranslationHandler
    public static class Translations
    {
        private static TextTranslation.Language _language;

        private static Dictionary<TextTranslation.Language, Dictionary<string, string>> _transDict = new()
        {
            // idea is to write code to populate from .xml later
            { TextTranslation.Language.ENGLISH, new Dictionary<string, string>()
            {
                { "IC_INTERACT", "Interact" },
                { "IC_BOARDMOVE", "Select Move" },
                { "IC_OVERHEAD", "Toggle Overhead" },
                { "IC_LEAN", "Lean Forward/Back" },
                { "IC_SILENCE", "..." },
                { "IC_WAIT", "Wait!" },
                { "IC_JK", "Uhh... nevermind."},
                { "IC_DONE", "I think I'm done playing." },
                { "IC_SHORTCUT", "a Shortcut" },
                { "IC_PLAYAGAIN", "Play Again" }
            } },
        };

        public static string GetTranslation(string text)
        {

            if (_transDict.TryGetValue(_language, out var table))
            {
                if (table.TryGetValue(text.ToUpper(), out var translation))
                    return translation;
            }
            else if (_transDict.TryGetValue(TextTranslation.Language.ENGLISH, out var eTable))
            {
                Logger.LogError($"Defaulting to English for {text}");
                if (eTable.TryGetValue(text, out var translation))
                    return translation;
            }

            Logger.LogError($"Defaulting to key for {text}");
            return null;

        }

        public static int GetUITextType(string text)
        {
            Dictionary<int, string> table = TextTranslation.Get().m_table.theUITable;
            _language = TextTranslation.Get().m_language;

            string transText = _transDict[_language][text.ToUpper()];

            int key = table.Keys.Max() + 1;
            try
            {
                // check to see if value already in table
                KeyValuePair<int, string> kvp = table.First(x => x.Value.Equals(transText));
                if (kvp.Equals(default(KeyValuePair<int, string>)))
                    key = kvp.Key;
            }
            catch (Exception) { }

            TextTranslation.Get().m_table.Insert_UI(key, transText);

            return key;
        }

        public static void UpdateLanguage()
        {
            _language = TextTranslation.Get().m_language;
        }

        public static void UpdateCharacterDialogue(CharacterDialogueTree dialogue)
        {
            TextTranslation.TranslationTable table = TextTranslation.Get().m_table;
            foreach (DialogueNode dnode in dialogue._mapDialogueNodes.Values)
            {
                // update w new page text
                List<DialogueText.TextBlock> blocks = dnode.DisplayTextData._listTextBlocks;
                foreach (DialogueText.TextBlock block in blocks)
                {
                    foreach (string page in block.listPageText)
                    {
                        // ignore if we're not adding a new line
                        if (table.Get(dnode.Name + page) == null)
                        {
                            string value = GetTranslation(page);
                            table.Insert(dnode.Name + page, value);
                        }
                    }
                }
                // update w new option text
                List<DialogueOption> options = dnode.ListDialogueOptions;
                foreach (DialogueOption option in options)
                {
                    if (table.Get(option._textID) == null)
                    {
                        string value = GetTranslation(option._text);
                        table.Insert(option._textID, value);
                    }
                }
            }
        }
    }
}
