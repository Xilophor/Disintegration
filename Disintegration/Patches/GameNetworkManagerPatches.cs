using HarmonyLib;

namespace Disintegration.Patches;

[HarmonyPatch]
[HarmonyWrapSafe]
internal class GameNetworkManagerPatches
{
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.StartHost))]
    [HarmonyPostfix]
    private static void StartHost()
    {
        Disintegration.EnableRoulette();
    }
    
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.StartClient))]
    [HarmonyPostfix]
    private static void StartClient()
    {
        Disintegration.EnableRoulette();
        NetworkMessageHandler.PingServer();
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect))]
    [HarmonyPostfix]
    private static void Disconnect()
    {
        Disintegration.DisableRoulette();
    }
}