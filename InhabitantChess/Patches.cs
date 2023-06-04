using HarmonyLib;
using InhabitantChess.Util;
using UnityEngine;
using static ItemTool;

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
            flag = flag || InhabitantChess.Instance.PlayerState == ChessPlayerState.Seated;

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
                else if (InhabitantChess.Instance.PlayerState == ChessPlayerState.Seated)
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

        // clamp camera position while leaning in seat
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.UpdateCamera))]
        public static void PlayerCameraController_UpdateCamera_Postfix(PlayerCameraController __instance)
        {
            if (InhabitantChess.Instance.PlayerState == ChessPlayerState.Seated)
            {
                float lean = InhabitantChess.Instance.GetLean();
                __instance._targetLocalPosition = new Vector3(__instance._targetLocalPosition.x, __instance._targetLocalPosition.y, lean);
                __instance.transform.localPosition = Vector3.Lerp(__instance.transform.localPosition, __instance._targetLocalPosition, 0.1f);
            }
        }

        // show shortcut prompt text
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemTool), nameof(ItemTool.UpdateState))]
        public static void ItemTool_UpdateState_Postfix(ItemTool __instance, PromptState newState, string itemName)
        {
            if (Locator.GetPlayerBody().GetComponentInChildren<PlayerSectorDetector>().IsWithinSector(Sector.Name.TimberHearth))
            {
                Shortcut shortcut = InhabitantChess.Instance.Shortcut;
                if (shortcut != null && !shortcut.UsedShortcut && itemName.Equals(shortcut.Lantern.GetDisplayName()))
                {
                    __instance._interactButtonPrompt.SetText(UITextLibrary.GetString(UITextType.TakePrompt) + " " +
                                                                Translations.GetTranslation("IC_SHORTCUT"));
                }
            }
        }

        // override Dreamworld ending
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DeathManager), nameof(DeathManager.BeginEscapedTimeLoopSequence))]
        public static bool DeathManager_BeginEscapedTimeLoopSequence_Prefix(TimeloopEscapeType escapeType)
        {
            if (escapeType == TimeloopEscapeType.Dreamworld)
            {
                InhabitantChess.Instance.PrisonerSequence.CanTriggerSequence = true;
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GlobalMusicController), nameof(GlobalMusicController.OnEnterDreamWorld))]
        public static void GlobalMusicController_OnEnterDreamWorld_Postfix(GlobalMusicController __instance)
        {
            if (PlayerState.IsResurrected() && __instance._playingFinalEndTimes)
            {
                __instance._playingFinalEndTimes = false;
                __instance._finalEndTimesDarkBrambleSource.FadeOut(1f);
                Locator.GetAudioMixer().UnmixEndTimes(5f);
            }
        }

        //
        // Patches from https://github.com/VioVayo/OWDreamWorldModAssist/blob/main/DWModAssist/Patches.cs
        //

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.StartRoasting))]
        public static void Campfire_StartRoasting_Postfix(Campfire __instance)
        {
            Shortcut.LastUsedCampfire = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.StartSleeping))]
        public static void Campfire_StartSleeping_Postfix(Campfire __instance)
        {
            Shortcut.LastUsedCampfire = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NomaiRemoteCameraPlatform), nameof(NomaiRemoteCameraPlatform.OnSocketableDonePlacing))]
        public static void NomaiRemoteCameraPlatform_SwitchToRemoteCamera_Postfix(NomaiRemoteCameraPlatform __instance)
        {
            Shortcut.LastUsedProjectionPool = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SlideProjector), nameof(SlideProjector.OnPressInteract))]
        public static void SlideProjector_OnPressInteract_Postfix(SlideProjector __instance)
        {
            Shortcut.LastUsedSlideProjector = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Peephole), nameof(Peephole.Peep))]
        public static void Peephole_Peep_Postfix(Peephole __instance)
        {
            Shortcut.LastUsedPeephole = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerAttachPoint), nameof(PlayerAttachPoint.AttachPlayer))]
        public static void PlayerAttachPoint_AttachPlayer_Postfix(PlayerAttachPoint __instance)
        {
            Shortcut.LastAttachedPoint = __instance;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(RingWorldController), nameof(RingWorldController.OnExitDreamWorld))]
        public static void RingWorldController_OnExitDreamWorld_Postfix()
        {
            Locator.GetCloakFieldController().OnPlayerEnter.Invoke();
        }

        [HarmonyPrefix] //Don't add player to AudioVolumes they don't spawn inside of
        [HarmonyPatch(typeof(DreamCampfire), nameof(DreamCampfire.OnExitDreamWorld))]
        public static bool DreamCampfire_OnExitDreamWorld_Prefix(DreamCampfire __instance)
        {
            var receiver = __instance._interactVolume as InteractReceiver;
            var distance = Vector3.Distance(Locator.GetPlayerTransform().position, receiver.gameObject.transform.position);
            return (distance < receiver._interactRange * 2.5f);
        }

    }
}
