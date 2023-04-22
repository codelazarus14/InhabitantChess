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
        public static TextTranslation.Language Language;

        private static Dictionary<TextTranslation.Language, Dictionary<ICText, string>> s_transDict = new Dictionary<TextTranslation.Language, Dictionary<ICText, string>>()
        {
            { TextTranslation.Language.ENGLISH, new Dictionary<ICText, string>()
            {
                { ICText.BoardMove, "Move" },
                { ICText.Wait, "Wait!" }
            } },
        };
        // enum to keep track of new strings to translate
        public enum ICText
        {
            BoardMove,
            Wait
        }

        public static string GetTranslation(ICText text)
        {

            if (s_transDict.TryGetValue(Language, out var table))
            {
                if (table.TryGetValue(text, out var translation))
                    return translation;
            }
            else if (s_transDict.TryGetValue(TextTranslation.Language.ENGLISH, out var eTable))
            {
                Logger.LogError($"Defaulting to English for {text}");
                if (eTable.TryGetValue(text, out var translation)) 
                    return translation;
            }

            Logger.LogError($"Defaulting to key for {text}");
            return null;

        }

        public static void UpdateUITable()
        {
            Dictionary<int, string> table = TextTranslation.Get().m_table.theUITable;

            for (int i = 0; i < s_transDict[Language].Count; i++)
            {
                string transText = s_transDict[Language].ElementAt(i).Value;

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
            }
        }
    }
}
