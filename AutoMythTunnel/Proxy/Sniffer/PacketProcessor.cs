using AutoMythTunnel.Proxy.Handler;
using ICSharpCode.SharpZipLib.Zip.Compression;
using MineSharp.Core.Common;
using MineSharp.Core.Common.Protocol;
using MineSharp.Core.Serialization;
using Spectre.Console;

namespace AutoMythTunnel.Proxy.Sniffer;

public class PacketProcessor(PacketQueue c2sQueue, PacketQueue s2cQueue, MinecraftStream clientStream, MinecraftStream serverStream, CancellationTokenSource? cancellationToken, int compression = 256, string? injectCommand = null)
{
    private const int CompressionDisabled = -1;
    private int compressionThreshold = compression;
    
    private readonly ChatHandler chatHandler = new(injectCommand)
    {
        protocolVersion = new(clientStream.protocolVersion)
    };
    
    public MinecraftStream clientStream { get; } = clientStream;
    public MinecraftStream serverStream { get; } = serverStream;

    public void Start()
    {
        cancellationToken ??= new CancellationTokenSource();
        ProtocolVersion protocolVersion = new(clientStream.protocolVersion);
        while (!cancellationToken.Token.IsCancellationRequested)
        {
            try
            {
                int localCompressionThreshold = compressionThreshold;
                int uncompressedLength = 0;
                PacketBuffer? packet = s2cQueue.Dequeue();
                if (packet != null)
                {
                    try
                    {
                        EnumPacketType? packetType = protocolVersion.ParsePacketType(EnumPacketWay.S2C, packet.ReadVarInt());
                        if (packetType == null) continue;
                        chatHandler.OnPacket(packetType.Value, packet, this);
                    }
                    catch
                    {
                        /*ignored*/
                    }
                }

                packet = c2sQueue.Dequeue();
                if (packet == null) continue;
                {
                    EnumPacketType? packetType = protocolVersion.ParsePacketType(EnumPacketWay.C2S, packet.ReadVarInt());
                    if (packetType == null) continue;
                    chatHandler.OnPacket(packetType.Value, packet, this);
                }
            } catch { /*ignore*/ }
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