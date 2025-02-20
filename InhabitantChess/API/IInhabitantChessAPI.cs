using UnityEngine;

namespace InhabitantChess.API
{
    public interface IInhabitantChessAPI
    {
        // TODO: add
        // - chess game event listener registration (start/end, sit/stand)

        // Returns the GameObject root of a newly created chess game
        public GameObject CreateChessGame(Transform parent, Pose localPose, bool canInteract = true);

        // Enables interaction with a specific chess game, given a GameObject originally returned from the above method
        public void EnableInteraction(GameObject game);

        // Disables interaction with a specific chess game, given a GameObject ... 
        // note that this does not force the player out of a game they are currently playing
        public void DisableInteraction(GameObject game);
    }
}
