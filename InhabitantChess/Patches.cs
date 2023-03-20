using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using OWML.ModHelper;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    [HarmonyPatch]
    public class Patches
    {
        // overriding player camera input for board game
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.UpdateInput))]
        public static bool PlayerCameraController_UpdateInput_Prefix(float deltaTime, PlayerCameraController __instance)
        {
            bool flag = __instance._shipController != null && __instance._shipController.AllowFreeLook() && OWInput.IsPressed(InputLibrary.freeLook, 0f);
            bool flag2 = OWInput.IsInputMode(InputMode.Character | InputMode.ScopeZoom | InputMode.NomaiRemoteCam | InputMode.PatchingSuit);
            if (__instance._isSnapping || __instance._isLockedOn || (PlayerState.InZeroG() && PlayerState.IsWearingSuit()) || (!flag2 && !flag))
            {
                return false;
            }
            bool flag3 = Locator.GetAlarmSequenceController() != null && Locator.GetAlarmSequenceController().IsAlarmWakingPlayer();
            Vector2 vector = Vector2.one;
            vector *= ((__instance._zoomed || flag3) ? PlayerCameraController.ZOOM_SCALAR : 1f);
            vector *= __instance._playerCamera.fieldOfView / __instance._initFOV;
            if (Time.timeScale > 1f)
            {
                vector /= Time.timeScale;
            }

            // detect if we're playing the game and treat camera input like cockpit
            flag = flag || InhabitantChess.Instance.Seated;

            if (flag)
            {
                Vector2 axisValue = OWInput.GetAxisValue(InputLibrary.look, InputMode.All);
                __instance._degreesX += axisValue.x * 180f * vector.x * deltaTime;
                __instance._degreesY += axisValue.y * 180f * vector.y * deltaTime;
                return false;
            }
            float num = (OWInput.UsingGamepad() ? PlayerCameraController.GAMEPAD_LOOK_RATE_Y : PlayerCameraController.LOOK_RATE);
            __instance._degreesY += OWInput.GetAxisValue(InputLibrary.look, InputMode.All).y * num * vector.y * deltaTime;
            return false;
        }

        // overriding camera adjustments for board game (x/y clamp constraints)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.UpdateRotation))]
        public static bool PlayerCameraController_UpdateRotation_Prefix(PlayerCameraController __instance)
        {
            __instance._degreesX %= 360f;
            __instance._degreesY %= 360f;
            if (!__instance._isSnapping)
            {
                if (__instance._shipController != null && __instance._shipController.AllowFreeLook() && OWInput.IsPressed(InputLibrary.freeLook, 0f))
                {
                    __instance._degreesX = Mathf.Clamp(__instance._degreesX, -60f, 60f);
                    __instance._degreesY = Mathf.Clamp(__instance._degreesY, -35f, 80f);
                }
                else if (InhabitantChess.Instance.Seated)
                {
                    __instance._degreesX = Mathf.Clamp(__instance._degreesX, -80f, 80f);
                    __instance._degreesY = Mathf.Clamp(__instance._degreesY, -80f, 35f);
                }
                else
                {
                    __instance._degreesX = 0f;
                    __instance._degreesY = Mathf.Clamp(__instance._degreesY, -80f, 80f);
                }
            }
            __instance._rotationX = Quaternion.AngleAxis(__instance._degreesX, Vector3.up);
            __instance._rotationY = Quaternion.AngleAxis(__instance._degreesY, -Vector3.right);
            Quaternion localRotation = __instance._rotationX * __instance._rotationY * Quaternion.identity;
            __instance._playerCamera.transform.localRotation = localRotation;
            return false;
        }
    }
}
