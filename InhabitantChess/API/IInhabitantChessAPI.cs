using UnityEngine;

namespace InhabitantChess.API
{
    public interface IInhabitantChessAPI
    {
        public ChessGame CreateChessGame(Transform parent, Pose localPose);
    }
}
