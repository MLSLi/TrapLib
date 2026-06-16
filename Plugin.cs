using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using TrapLib.MP;
using TrapLib.Patches;
using TrapLib.Utilities;

namespace TrapLib;

[BepInPlugin("com.vertigo.traplib", "TrapLib", "1.0.2")]
[BepInDependency("com.rushellxyz.rshlib", BepInDependency.DependencyFlags.SoftDependency)]
public class TrapLibPlugin : BaseUnityPlugin
{
    internal static BepInEx.Logging.ManualLogSource Log;

    internal static bool KrokMpEnabled;
    internal static bool RshLibInstalled;

    private void Awake()
    {
        Log = Logger;

        KrokMpEnabled = Chainloader.PluginInfos.ContainsKey("KrokoshaCasualtiesMP");
        RshLibInstalled = Chainloader.PluginInfos.ContainsKey("com.rushellxyz.rshlib");
        MPSync.Refresh();

        Log.LogInfo($"TrapLib 1.0.2 — KrokMP:{KrokMpEnabled} RshLib:{RshLibInstalled}");

        var harmony = new Harmony("com.vertigo.traplib");
        harmony.PatchAll();

        if (!RshLibInstalled)
            BuildingEntityPatch.Apply(harmony);

        RegisterSounds();
    }

    public static void RegisterSounds()
    {
        foreach (var entry in TrapRegistry.Entries.Values)
            TrapSounds.Map[entry.config.Id] = (entry.config.Sounds.hit, entry.config.Sounds.destroy);
    }
}
