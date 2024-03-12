using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using StaticNetcodeLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = System.Random;

namespace Disintegration;

// StaticNetcode Attribute to let StaticNetcodeLib know to search for static rpcs in this class.
// This class also does not have to inherit from NetworkBehaviour, unlike typical NGO rpcs.
[StaticNetcode]
public class RouletteWheel : MonoBehaviour
{
    private double _timeUntilChop;

    private static Random _random = null!;

    private static MethodInfo[]? _enabledMethods;
    private static MethodInfo[]? _disabledMethods;

    #region Static Rpcs

    // Ensure that the rpcs are static for StaticNetcodeLib to patch them
    [ServerRpc]
    internal static void PingServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var clientId = serverRpcParams.Receive.SenderClientId;
        
        PingClientRpc(_disabledMethods!, new ClientRpcParams { Send = { TargetClientIds = [ clientId ] } });
    }

    [ClientRpc]
    private static void PingClientRpc(IEnumerable<MethodInfo> disabledMethods, ClientRpcParams clientRpcParams)
    {
        Disintegration.DisableRoulette();
        disabledMethods.Do(Disintegration.RoulettePatch);
    }

    [ClientRpc]
    private static void DisableClientRpc(MethodInfo methodInfo)
    {
        Disintegration.RoulettePatch(methodInfo);
    }

    #endregion

    private void FixedUpdate()
    {
        if (_enabledMethods is null or { Length: 0 }) { gameObject.SetActive(false); return; }
        if (_timeUntilChop > 0) { _timeUntilChop -= Time.fixedDeltaTime; return; }
            
        _timeUntilChop = _random.Next(4, 18);

        Disintegration.RoulettePatch(_enabledMethods[0]);

        if (NetworkManager.Singleton is { IsServer: true })
            DisableClientRpc(_enabledMethods[0]);

        _disabledMethods.AddToArray(_enabledMethods[0]);
        _enabledMethods = _enabledMethods.RemoveAt(0);
    }

    #region Enable & Disable

    private static readonly MethodInfo[] BlacklistFromRoulette = [
        // Allow Disconnecting from the Game
        AccessTools.Method(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect)),
        AccessTools.Method(typeof(GameNetworkManager), nameof(GameNetworkManager.OnApplicationQuit)),
        AccessTools.Method(typeof(QuickMenuManager), nameof(QuickMenuManager.LeaveGame)),
        AccessTools.Method(typeof(QuickMenuManager), nameof(QuickMenuManager.LeaveGameConfirm)),
        AccessTools.Method(typeof(QuickMenuManager), nameof(QuickMenuManager.OpenQuickMenu)),
        AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.OpenMenu_performed))
    ];
    
    private void OnEnable()
    {
        _random = new Random();
            
        _timeUntilChop = _random.Next(2, 15);
            
        _enabledMethods = AccessTools.GetTypesFromAssembly(Assembly.GetAssembly(typeof(RoundManager)))
            .Select(type => type.GetMethods(AccessTools.allDeclared))
            .SelectMany(method => method).ToArray()
            .AddRange(
                Chainloader.PluginInfos.Values
                    .Where(info => info.Metadata.GUID is not MyPluginInfo.PLUGIN_GUID and StaticNetcodeLib.StaticNetcodeLib.Guid)
                    .SelectMany(info =>
                        AccessTools.GetTypesFromAssembly(info.Instance.GetType().Assembly)
                            .Select(type => type.GetMethods(AccessTools.allDeclared))
                            .SelectMany(method => method)).ToArray())
            .Where(methodInfo => !BlacklistFromRoulette
                .Any(blacklistedMethod => blacklistedMethod.Equals(methodInfo)))
            .OrderBy(_ => _random.Next()).ToArray();
        
        _disabledMethods = [];
    }

    private void OnDisable() => _enabledMethods = _disabledMethods = [];

    #endregion
}