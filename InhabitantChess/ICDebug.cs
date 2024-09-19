using UnityEngine;

namespace InhabitantChess
{
    public class ICDebug : MonoBehaviour
    {
        // TODO: test api from here

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

            // TODO: consolidate all input bindings somewhere
            if (OWInput.IsNewlyPressed(InputLibrary.enter))
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

                    InhabitantChess.InstantiateChessGame(hit.rigidbody.transform, new Pose(pos, rot));
                    Util.Logger.LogSuccess($"Instantiated chess game at {hit.rigidbody.transform}: {pos}");
                }
            }
        }

        private void OnConfigure()
        {
            bool canSpawn = InhabitantChess.Debugging && InhabitantChess.CurrentGame == null;
            ScreenPromptController.Instance.SetPromptVisibility(ScreenPromptController.PromptType.SpawnChessGame, canSpawn);
        }
    }
}
