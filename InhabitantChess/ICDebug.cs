using InhabitantChess.API;
using UnityEngine;

namespace InhabitantChess
{
    public class ICDebug : MonoBehaviour
    {
        public IInhabitantChessAPI MyAPI;

        private InhabitantChess InhabitantChess => InhabitantChess.Instance;

        private void Start()
        {
            InhabitantChess.OnConfigure += OnConfigure;
            OnConfigure();
        }

        private void OnDestroy()
        {
            InhabitantChess.OnConfigure -= OnConfigure;
        }

        private void Update()
        {
            if (!InhabitantChess.Debugging || InhabitantChess.CurrentGame != null) return;

            if (OWInput.IsNewlyPressed(Controls.SpawnGame))
            {
                Transform manipTrans = Locator.GetPlayerTransform().GetComponentInChildren<FirstPersonManipulator>().transform;
                if (Physics.Raycast(manipTrans.position, manipTrans.forward, out RaycastHit hit, 75f, OWLayerMask.physicalMask))
                {
                    // copied from NH debug raycast
                    // https://github.com/Outer-Wilds-New-Horizons/new-horizons/blob/main/NewHorizons/Utility/DebugTools/DebugRaycaster.cs
                    Vector3 worldSpacePos = hit.point + hit.normal; // offset from point a little
                    Vector3 pos = hit.rigidbody.transform.InverseTransformPoint(worldSpacePos);

                    Vector3 toOrigin = Vector3.ProjectOnPlane((manipTrans.position - hit.point).normalized, hit.normal);
                    Quaternion worldSpaceRot = Quaternion.LookRotation(toOrigin, hit.normal);
                    Quaternion rot = hit.rigidbody.transform.InverseTransformRotation(worldSpaceRot);

                    MyAPI.CreateChessGame(hit.rigidbody.transform, new Pose(pos, rot));
                }
            }
        }

        private void TestEnable(ChessGame game)
        {
            MyAPI.EnableInteraction(game);
        }

        private void TestDisable(ChessGame game)
        {
            MyAPI.DisableInteraction(game);
        }

        private void OnConfigure()
        {
            bool canSpawn = InhabitantChess.Debugging && InhabitantChess.CurrentGame == null;
            ScreenPromptController.Instance.SetPromptVisibility(ScreenPromptController.PromptType.SpawnChessGame, canSpawn);
        }
    }
}
