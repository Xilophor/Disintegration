using System.Reflection;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace Disintegration;

internal static class NetworkMessageHandler
{
    private const string PingMessage = $"{MyPluginInfo.PLUGIN_GUID}.Ping";
    private const string MethodMessage = $"{MyPluginInfo.PLUGIN_GUID}.Methods";
    
    internal static void Start()
    {
        NetworkManager.Singleton.CustomMessagingManager
            .RegisterNamedMessageHandler(PingMessage, ReceivePing);
        NetworkManager.Singleton.CustomMessagingManager
            .RegisterNamedMessageHandler(MethodMessage, ReceiveMethodMessage);
    }

    internal static void Disconnect()
    {
        NetworkManager.Singleton.CustomMessagingManager
            .UnregisterNamedMessageHandler(PingMessage);
        NetworkManager.Singleton.CustomMessagingManager
            .UnregisterNamedMessageHandler(MethodMessage);
    }

    private static void ReceivePing(ulong client, FastBufferReader _)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(PingMessage,
                client,
                new FastBufferWriter());

            var writer = new FastBufferWriter(8196, Allocator.Temp);

            foreach (var method in RouletteWheel.DisabledMethods ?? [])
                SerializeMethod(ref writer, method);
            
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MethodMessage,
                client,
                writer, 
                NetworkDelivery.ReliableFragmentedSequenced);
        }
        else
        {
            Disintegration.DisableRoulette();
        }
    }

    private static void ReceiveMethodMessage(ulong client, FastBufferReader reader)
    {
        reader.ReadValue(out int numOfMethods);

        for (var i = 0; i < numOfMethods; i++)
        {
            reader.ReadValue(out string methodType);
            reader.ReadValue(out string methodName);
            Disintegration.RoulettePatch(AccessTools.Method(AccessTools.TypeByName(methodType), methodName));
        }
        
        reader.Dispose();
    }

    private static void SerializeMethod(ref FastBufferWriter writer, MemberInfo methodInfo)
    {
        writer.WriteValue(methodInfo.DeclaringType!.FullName);
        writer.WriteValue(methodInfo.Name);
    }

    internal static void SendMethodToDisable(MemberInfo methodInfo)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var writer = new FastBufferWriter(1024, Allocator.Temp);
        
        SerializeMethod(ref writer, methodInfo);
        
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MethodMessage,
            NetworkManager.Singleton.ConnectedClientsIds,
            writer);
    }

    internal static void PingServer()
    {
        var writer = new FastBufferWriter();
        
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(PingMessage,
            NetworkManager.ServerClientId,
            writer);
    }
}