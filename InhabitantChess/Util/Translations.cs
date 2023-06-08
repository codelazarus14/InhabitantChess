using Newtonsoft.Json;
using OWML.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InhabitantChess.Util
{
    // borrowed from NH TranslationHandler
    public static class Translations
    {
        private static TextTranslation.Language _language;
        private static Dictionary<TextTranslation.Language, Dictionary<string, string>> _transDict = new();

        public static void LoadTranslations()
        {
            var folder = Path.Combine(InhabitantChess.Instance.ModHelper.Manifest.ModFolderPath, "Assets/Translations/");
            if (Directory.Exists(folder))
            {
                foreach (var language in EnumUtils.GetValues<TextTranslation.Language>())
                {
                    var file = Path.Combine(folder, "ic_" + language.ToString().ToLower() + ".json");
                    if (File.Exists(file))
                    {
                        using StreamReader reader = new(file);
                        string translation = reader.ReadToEnd();
                        _transDict.Add(language, JsonConvert.DeserializeObject<Dictionary<string, string>>(translation));
                    }
                }
                string transLangs = "";
                foreach (var l in _transDict.Keys) transLangs += l.ToString() + ", ";
                transLangs = transLangs.Trim().TrimEnd(',');
                Logger.Log($"Translations for {_transDict.Count} language{(_transDict.Count > 1 ? "s" : "")} loaded: {transLangs}");
            }
            else Logger.LogError($"Missing Assets/Translations/ folder!");
        }

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
