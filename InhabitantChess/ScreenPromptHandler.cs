using InhabitantChess.Util;
using UnityEngine;

namespace InhabitantChess
{
    public class ScreenPromptHandler : MonoBehaviour
    {
        private ScreenPrompt _interactPrompt;
        private ButtonPromptLibrary _promptButtons = ButtonPromptLibrary.SharedInstance;

        private void Start()
        {
            // testing functionality - ideally we want a bunch of methods to control adding/removing prompts as necessary
            if (_interactPrompt == null)
            {
                _interactPrompt = new ScreenPrompt(Translations.GetTranslation(Translations.ICText.BoardMove) + "<CMD>", GetButtonSprite(KeyCode.E));
                Locator.GetPromptManager().AddScreenPrompt(_interactPrompt, PromptPosition.UpperRight, true);
            }
        }

        private Sprite GetButtonSprite(KeyCode key)
        {
            Texture2D texture = _promptButtons.GetButtonTexture(key);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = texture.name;
            return sprite;
        }
    }
}
