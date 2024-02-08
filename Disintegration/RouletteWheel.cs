using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = System.Random;

namespace Disintegration;

public class RouletteWheel : MonoBehaviour
{
    private double _timeUntilChop;
        
    private static Random _random = null!;

    private static readonly MethodInfo[] BlacklistFromRoulette = [
        // Allow Disconnecting from the Game
        AccessTools.Method(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect)),
        AccessTools.Method(typeof(GameNetworkManager), nameof(GameNetworkManager.OnApplicationQuit)),
        AccessTools.Method(typeof(QuickMenuManager), nameof(QuickMenuManager.LeaveGame)),
        AccessTools.Method(typeof(QuickMenuManager), nameof(QuickMenuManager.LeaveGameConfirm)),
        AccessTools.Method(typeof(QuickMenuManager), nameof(QuickMenuManager.OpenQuickMenu)),
        AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.OpenMenu_performed))
    ];
    
    internal static MethodInfo[]? EnabledMethods;
    internal static MethodInfo[]? DisabledMethods;

    private void OnEnable()
    {
        _random = new Random();
            
        _timeUntilChop = _random.Next(2, 15);
            
        EnabledMethods = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(RoundManager)))
            .Select(type => type.GetMethods(AccessTools.allDeclared))
            .SelectMany(method => method).ToArray()
            .AddRange(
                Chainloader.PluginInfos.Values
                    .Where(info => info.Metadata.GUID != MyPluginInfo.PLUGIN_GUID)
                    .SelectMany(info =>
                        AccessTools.GetTypesFromAssembly(info.Instance.GetType().Assembly)
                            .Select(type => type.GetMethods(AccessTools.allDeclared))
                            .SelectMany(method => method)).ToArray())
            .Where(methodInfo => !BlacklistFromRoulette
                .Any(blacklistedMethod => blacklistedMethod.Equals(methodInfo)))
            .OrderBy(_ => _random.Next()).ToArray();
        
        DisabledMethods = [];
    }

    private void OnDisable() => EnabledMethods = DisabledMethods = [];

    private void FixedUpdate()
    {
        if (EnabledMethods is null or { Length: 0 }) { gameObject.SetActive(false); return; }
        if (_timeUntilChop > 0) { _timeUntilChop -= Time.fixedDeltaTime; return; }
            
        _timeUntilChop = _random.Next(4, 18);

        Disintegration.RoulettePatch(EnabledMethods[0]);

        if (NetworkManager.Singleton is { IsServer: true })
            NetworkMessageHandler.SendMethodToDisable(EnabledMethods[0]);

        DisabledMethods.AddToArray(EnabledMethods[0]);
        EnabledMethods = EnabledMethods.RemoveAt(0);
    }
}