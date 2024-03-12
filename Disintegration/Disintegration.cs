using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Disintegration;

// BepInDependency Attribute to let StaticNetcodeLib know to search within this mod.
[BepInDependency(StaticNetcodeLib.StaticNetcodeLib.Guid)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Disintegration : BaseUnityPlugin
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public static Disintegration Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    
    private static readonly Harmony RouletteHarmony = new($"{MyPluginInfo.PLUGIN_GUID}.Roulette");
    
    private static Harmony? _harmony;
    internal static GameObject RouletteWheelObject = null!;
    private static readonly HarmonyMethod PrefixMethod = new(typeof(Disintegration), nameof(Prefix));

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        RouletteWheelObject = new GameObject("RouletteWheel")
            { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(RouletteWheelObject);
        RouletteWheelObject.SetActive(false);
        RouletteWheelObject.AddComponent<RouletteWheel>();

        Patch();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private static void Patch()
    {
        _harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");

        _harmony.PatchAll();

        Logger.LogDebug("Finished patching!");
    }

    internal static void RoulettePatch(MethodInfo methodInfo)
    {
        try
        {
            RouletteHarmony.Patch(methodInfo, prefix: PrefixMethod);
#if DEBUG
            Logger.LogDebug($"removed {methodInfo.DeclaringType!.FullName}::{methodInfo.Name}");
#endif
        }
        catch (Exception e)
        {
#if DEBUG
            Logger.LogDebug($"failed to remove {methodInfo.DeclaringType!.FullName}::{methodInfo.Name}");
            Logger.LogDebug(e);
#endif
        }
    }

    internal static void EnableRoulette() => RouletteWheelObject.SetActive(true);

    internal static void DisableRoulette()
    {
        RouletteWheelObject.SetActive(false);
        RouletteHarmony.UnpatchSelf();
    }

    private static bool Prefix() => false;
}