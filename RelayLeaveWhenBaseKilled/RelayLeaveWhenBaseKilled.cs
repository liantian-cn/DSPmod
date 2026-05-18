using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace RelayLeaveWhenBaseKilled
{
    [BepInPlugin("me.liantian.plugin.RelayLeaveWhenBaseKilled", "RelayLeaveWhenBaseKilled", "0.0.1")]
    public class RelayLeaveWhenBaseKilled : BaseUnityPlugin
    {
        private const string PluginGuid = "me.liantian.plugin.RelayLeaveWhenBaseKilled";
        private const string PluginName = "RelayLeaveWhenBaseKilled";
        private const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private Harmony harmony;

        public void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(RelayLeaveWhenBaseKilled).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} initialized");
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }

        [HarmonyPatch(typeof(EnemyDFGroundSystem), "NotifyBaseKilled")]
        private static class EnemyDFGroundSystemNotifyBaseKilledPatch
        {
            private static void Postfix(EnemyDFGroundSystem __instance, int baseId)
            {
                if (__instance?.bases?.buffer == null || baseId <= 0 || baseId >= __instance.bases.cursor)
                {
                    return;
                }

                DFGBaseComponent baseComponent = __instance.bases.buffer[baseId];
                if (baseComponent == null || baseComponent.id != baseId)
                {
                    return;
                }

                DFRelayComponent relay = baseComponent.GetRelay();
                if (relay == null)
                {
                    return;
                }

                bool isRelayBoundToKilledBase = relay.baseId == baseId;
                bool isLandedRelay = relay.stage == 2;
                bool isMaintainingGroundBase = relay.baseState == 2;
                if (!isRelayBoundToKilledBase || !isLandedRelay || !isMaintainingGroundBase)
                {
                    return;
                }

                relay.LeaveBase();
                if (relay.hive != null)
                {
                    relay.hive.relayNeutralizedCounter++;
                }
            }
        }
    }
}
