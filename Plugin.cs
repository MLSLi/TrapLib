using BepInEx;
using HarmonyLib;
using TrapLib.MP;
using TrapLib.Patches;

namespace TrapLib;

[BepInPlugin("com.vertigo.traplib", "TrapLib", "1.1.3")]
[BepInDependency("KrokoshaCasualtiesMP", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.rushellxyz.rshlib", BepInDependency.DependencyFlags.SoftDependency)]
public class TrapLibPlugin : BaseUnityPlugin
{
    internal static BepInEx.Logging.ManualLogSource Log;

    internal static bool KrokMpEnabled;
    internal static bool RshLibInstalled;

    private void Awake()
    {
        Log = Logger;

        KrokMpEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("KrokoshaCasualtiesMP");
        RshLibInstalled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rushellxyz.rshlib");
        MPSync.Refresh();

        Log.LogInfo($"TrapLib 1.1.3 — KrokMP:{KrokMpEnabled} RshLib:{RshLibInstalled}");

        var harmony = new Harmony("com.vertigo.traplib");
        harmony.PatchAll();

        if (!RshLibInstalled)
            BuildingEntityPatch.Apply(harmony);

        // TrapSounds.Map is now lazily populated on first access — no explicit call needed.
    }
}
