using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess.Util
{
    // borrowed from NH TranslationHandler
    public static class Translations
    {
        private static TextTranslation.Language _language;

        private static Dictionary<TextTranslation.Language, Dictionary<ICText, string>> _transDict = new()
        {
            // idea is to write code to populate from .xml later
            { TextTranslation.Language.ENGLISH, new Dictionary<ICText, string>()
            {
                { ICText.Interact, "Interact" },
                { ICText.BoardMove, "Select Move" },
                { ICText.Overhead, "Toggle Overhead" },
                { ICText.LeanForward, "Lean Forward" }
            } },
        };
        // enum to keep track of new strings to translate
        public enum ICText
        {
            Interact,
            BoardMove, 
            Overhead,
            LeanForward,
        }

        public static string GetTranslation(ICText text)
        {

            if (_transDict.TryGetValue(_language, out var table))
            {
                if (table.TryGetValue(text, out var translation))
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

        public static int GetUITextType(ICText text)
        {
            Dictionary<int, string> table = TextTranslation.Get().m_table.theUITable;
            _language = TextTranslation.Get().m_language;

            string transText = _transDict[_language][text];

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
    }
}
