using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using UXAssist.UI;
using UXAssist.Common;

namespace SurfaceRuins
{
    [BepInPlugin("me.liantian.plugin.SurfaceRuins", "SurfaceRuins", "0.0.1")]
    public class SurfaceRuins : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.SurfaceRuins";
        private const string PluginName = "SurfaceRuins";
        private const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private Harmony harmony;

    }
}
