using AutoMythTunnel.Proxy.Handler;
using ICSharpCode.SharpZipLib.Zip.Compression;
using MineSharp.Core.Common;
using MineSharp.Core.Common.Protocol;
using MineSharp.Core.Serialization;
using Spectre.Console;

namespace AutoMythTunnel.Proxy.Sniffer;

public class PacketProcessor(PacketQueue c2sQueue, PacketQueue s2cQueue, MinecraftStream clientStream, MinecraftStream serverStream, CancellationTokenSource? cancellationToken, int compression = 256, string? injectCommand = null, bool offline = false)
{
    private const int CompressionDisabled = -1;
    private int compressionThreshold = compression;
    
    private readonly ChatHandler chatHandler = new(injectCommand)
    {
        protocolVersion = new(clientStream.protocolVersion),
        offline = offline
    };
    
    public MinecraftStream clientStream { get; } = clientStream;
    public MinecraftStream serverStream { get; } = serverStream;

    public void Start()
    {
        cancellationToken ??= new CancellationTokenSource();
        ProtocolVersion protocolVersion = new(clientStream.protocolVersion);
        List<Task> tasks =
        [
            Task.Run(() => ProcessS2C(protocolVersion), cancellationToken.Token),
            Task.Run(() => ProcessC2S(protocolVersion), cancellationToken.Token)
        ];
        Task.WaitAll(tasks.ToArray());
    }
    
    private void ProcessS2C(ProtocolVersion protocolVersion)
    {
        while (!cancellationToken!.Token.IsCancellationRequested)
        {
            PacketBuffer? packet = s2cQueue.Dequeue();
            if (packet == null) continue;
            {
                EnumPacketType? packetType = protocolVersion.ParsePacketType(EnumPacketWay.S2C, packet.ReadVarInt());
                if (packetType == null) continue;
                chatHandler.OnPacket(packetType.Value, packet, this);
            }
        }
    }

    private void ProcessC2S(ProtocolVersion protocolVersion)
    {
        while (!cancellationToken!.Token.IsCancellationRequested)
        {
            PacketBuffer? packet = c2sQueue.Dequeue();
            if (packet == null) continue;
            {
                EnumPacketType? packetType = protocolVersion.ParsePacketType(EnumPacketWay.C2S, packet.ReadVarInt());
                if (packetType == null) continue;
                chatHandler.OnPacket(packetType.Value, packet, this);
            }
        }
    }
    
    public void Stop()
    {
        cancellationToken.Cancel();
    }
    
    public void WriteToServer(PacketBuffer buffer)
    {
        serverStream.WritePacket(buffer);
    }
    
    public void WriteToClient(PacketBuffer buffer)
    {
        clientStream.WritePacket(buffer);
    }
}