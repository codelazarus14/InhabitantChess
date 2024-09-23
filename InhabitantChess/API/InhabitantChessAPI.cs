using UnityEngine;

namespace InhabitantChess.API
{
    public class InhabitantChessAPI : IInhabitantChessAPI
    {
        public ChessGame CreateChessGame(Transform parent, Pose localPose, bool canInteract = true)
        {
            Util.Logger.Log($"Creating chess game with parent ({parent.name}), position {localPose.position}, rotation {localPose.rotation}");
            return InhabitantChess.Instance.InstantiateChessGame(parent, localPose, canInteract);
        }

        public void DisableInteraction(ChessGame game)
        {
            Util.Logger.Log($"Disabling interaction for {game}");
            game.DisableInteraction();
        }

        public void EnableInteraction(ChessGame game)
        {
            Util.Logger.Log($"Enabling interaction for {game}");
            game.EnableInteraction();
        }
    }
}
