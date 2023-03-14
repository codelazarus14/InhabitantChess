using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Locator), nameof(Locator.OnSwitchActiveCamera))]
        public static void Locator_OnSwitchActiveCamera_Postfix(OWCamera activeCamera)
        {
            Logger.Log($"Main camera switched to {activeCamera}");
        }
    }
}
