using UnityEngine;

namespace InhabitantChess.API
{
    public class InhabitantChessAPI : IInhabitantChessAPI
    {
        public GameObject CreateChessGame(Transform parent, Pose localPose, bool canInteract = true)
        {
            Util.Logger.Log($"Creating chess game with parent ({parent.name}), position {localPose.position}, rotation {localPose.rotation}");
            return InhabitantChess.Instance.InstantiateChessGame(parent, localPose, canInteract).gameObject;
        }

        public void DisableInteraction(GameObject game)
        {
            if (game.TryGetComponent(out ChessGame cg))
            {
                Util.Logger.Log($"Disabling interaction for {cg}");
                cg.DisableInteraction();
            }
            else
                Util.Logger.LogError($"Can't disable: GameObject '{game}' does not have a ChessGame attached!");
        }

        public void EnableInteraction(GameObject game)
        {
            if (game.TryGetComponent(out ChessGame cg))
            {
                Util.Logger.Log($"Enabling interaction for {cg}");
                cg.EnableInteraction();
            }
            else
                Util.Logger.LogError($"Can't enable: GameObject '{game}' does not have a ChessGame attached!");
        }
    }
}
