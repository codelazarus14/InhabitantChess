using UnityEngine;

namespace InhabitantChess.API
{
    public interface IInhabitantChessAPI
    {
        public ChessGame CreateChessGame(Transform parent, Pose localPose, bool canInteract = true);
        public void EnableInteraction(ChessGame game);
        public void DisableInteraction(ChessGame game);
    }
}
