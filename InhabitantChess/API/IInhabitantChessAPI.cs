using UnityEngine;

namespace InhabitantChess.API
{
    public interface IInhabitantChessAPI
    {
        // TODO: add
        // - chess game event listener registration (start/end, sit/stand)
        public ChessGame CreateChessGame(Transform parent, Pose localPose, bool canInteract = true);
        public void EnableInteraction(ChessGame game);
        public void DisableInteraction(ChessGame game);
    }
}
