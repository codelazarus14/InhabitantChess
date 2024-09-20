using UnityEngine;

namespace InhabitantChess.API
{
    public class InhabitantChessAPI : IInhabitantChessAPI
    {
        public ChessGame CreateChessGame(Transform parent, Pose localPose)
        {
            Util.Logger.Log($"Creating chess game with parent ({parent.name}), position {localPose.position}, rotation {localPose.rotation}");
            return InhabitantChess.Instance.InstantiateChessGame(parent, localPose);
        }
    }
}
