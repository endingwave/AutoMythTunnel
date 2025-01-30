using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AutoMythTunnel.Data;
using AutoMythTunnel.Proxy.Sniffer;
using AutoMythTunnel.Utils;
using ICSharpCode.SharpZipLib.Zip.Compression;
using MineSharp.Auth.Exceptions;
using MineSharp.Core.Common;
using MineSharp.Core.Common.Protocol;
using MineSharp.Core.Serialization;
using MineSharp.Protocol.Packets.Serverbound.Login;
using SocksSharp.Proxy;
using Spectre.Console;
using static System.Net.IPAddress;

namespace AutoMythTunnel.Proxy;

public class ProxyServer(string minecraftAccessToken, ProxySettings? proxySettings = null, string targetServer = "mc.hypixel.net", int targetPort = 25565, int localPort = 25565, bool rewriteServerAddress = false, string? injectCommand = null)
{
    private MinecraftProfile? _profile;
    private TcpListener? _listener;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly Deflater deflater = new();
    private int compressionThreshold = -1;
    public Action<string>? OnJoin { get; set; }

    public void Start()
    {
        // get profile
        _profile = MicrosoftApi.GetMcProfile(minecraftAccessToken);
        _listener = new TcpListener(Any, localPort);
        _listener.Start();
        if (localPort == 25565) {
            new Thread(BroadcastLan).Start();
        }
        new Task((() =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_listener.Pending())
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    new Thread(() => HandleClient(client)).Start();
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        })).Start();
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _listener?.Stop();
    }

    private void BroadcastLan()
    {
        UdpClient udpClient = new();
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            Thread.Sleep(1500);
            byte[] data = "[MOTD]Tunnel Connector by EndingWave[/MOTD][AD]25565[/AD]"u8.ToArray();
            udpClient.Send(data, data.Length, new IPEndPoint(Parse("224.0.2.60"), 4445));
        }
    }
    
    private void HandleStatusConnection(MinecraftStream minecraftStream, TcpClient client)
    {
        while (true)
        {
            PacketBuffer packet = minecraftStream.ReadPacket();
            int packetId = packet.ReadVarInt();
            switch (packetId)
            {
                case 0:
                {
                    PacketBuffer response = new(minecraftStream.protocolVersion);
                    response.WriteVarInt(0);
                    response.WriteString("{\"version\":{\"name\":\"EndingWaveProxy\",\"protocol\":" + minecraftStream.protocolVersion + "},\"players\":{\"max\":20,\"online\":0},\"description\":{\"text\":\"§dTunnel §6Connector §bby §dEndingWave\n§dName: §b" + _profile!.Name + "\"}}");
                    minecraftStream.WritePacket(response);
                    break;
                }
                case 1:
                    // sleep 20ms
                    Thread.Sleep(20);
                    minecraftStream.WritePacket(packet);
                    return; // should be the last packet
                default:
                    AnsiConsole.MarkupLine($"Warn: Invalid packet received from {client.Client.RemoteEndPoint}.");
                    client.Dispose();
                    return;
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        try {
            GameState state;
            using NetworkStream clientStream = client.GetStream();
            NetworkStream serverStream;
            MinecraftStream clientMinecraftStream = new(clientStream, 754);
            PacketBuffer firstPacket = clientMinecraftStream.ReadPacket();
            string hwid = HardwareIdGenerator.Generate();
            if (firstPacket.ReadVarInt() == 0)
            {
                int protocolVersion = firstPacket.ReadVarInt();
                firstPacket.ReadString(); // Server address
                firstPacket.ReadUShort(); // Server port
                int nextState = firstPacket.ReadVarInt();
                switch (nextState)
                {
                    case 1:
                        state = GameState.Status;
                        break;
                    case 2:
                    case 3:
                        state = GameState.Login;
                        AnsiConsole.MarkupLine($"Info: Client {client.Client.RemoteEndPoint} try to join server.");
                        break;
                    default:
                        AnsiConsole.MarkupLine($"Warn: Invalid packet received from {client.Client.RemoteEndPoint}.");
                        client.Dispose();
                        return;
                }

                clientMinecraftStream.protocolVersion = protocolVersion;
            }
            else
            {
                AnsiConsole.MarkupLine($"Warn: Invalid packet received from {client.Client.RemoteEndPoint}.");
                client.Dispose();
                return;
            }
            if (state == GameState.Status)
            {
                HandleStatusConnection(clientMinecraftStream, client);
                return;
            }

            while (true)
            {
                PacketBuffer buffer = clientMinecraftStream.ReadPacket();
                int packetId = buffer.ReadVarInt();
                switch (packetId)
                {
                    case 0:
                        AnsiConsole.MarkupLine($"Info: Login start packet received from {client.Client.RemoteEndPoint}.");
                        bool supported = ((List<int>) [47, 340]).Contains(clientMinecraftStream.protocolVersion);
                        bool sendKeepAlive = supported;
                        if (supported)
                        {
                            ProtocolVersion _protocolVersion =
                                new ProtocolVersion(clientMinecraftStream.protocolVersion);
                            // response login success
                            PacketBuffer loginSuccess = new(clientMinecraftStream.protocolVersion);
                            loginSuccess.WriteVarInt(2);
                            // write uuid with hyphen
                            loginSuccess.WriteString(Uuid.Parse(_profile!.Uuid).ToString());
                            loginSuccess.WriteString(_profile.Name);
                            clientMinecraftStream.WritePacket(loginSuccess);
                            Thread.Sleep(50);
                            // send join game packet
                            PacketBuffer joinGame = new(clientMinecraftStream.protocolVersion);
                            joinGame.WriteVarInt(_protocolVersion.ParsePacketId(EnumPacketType.SERVER_JOIN_GAME));
                            joinGame.WriteInt(100);
                            joinGame.WriteByte(0);
                            if (clientMinecraftStream.protocolVersion == 47)
                            {
                                joinGame.WriteByte(0);
                            }
                            else
                            {
                                joinGame.WriteInt(0);
                            }
                            joinGame.WriteByte(0);
                            joinGame.WriteByte(20);
                            joinGame.WriteString("default");
                            joinGame.WriteBool(false);
                            clientMinecraftStream.WritePacket(joinGame);
                            Thread.Sleep(50);
                            // Send player pos look packet to close download screen
                            PacketBuffer playerPosLook = new(clientMinecraftStream.protocolVersion);
                            playerPosLook.WriteVarInt(_protocolVersion.ParsePacketId(EnumPacketType.SERVER_SET_POSITION_AND_ROTATION));
                            playerPosLook.WriteDouble(0);
                            playerPosLook.WriteDouble(0);
                            playerPosLook.WriteDouble(0);
                            playerPosLook.WriteFloat(0);
                            playerPosLook.WriteFloat(0);
                            playerPosLook.WriteByte(0);
                            if (clientMinecraftStream.protocolVersion == 340)
                            {
                                playerPosLook.WriteVarInt(1);
                            }
                            clientMinecraftStream.WritePacket(playerPosLook);
                            Thread.Sleep(500);
                            // set title display
                            PacketBuffer titleDisplayTime = new(clientMinecraftStream.protocolVersion);
                            titleDisplayTime.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x45 : 0x48);
                            titleDisplayTime.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 2 : 3);
                            titleDisplayTime.WriteInt(3);
                            titleDisplayTime.WriteInt(30);
                            titleDisplayTime.WriteInt(3);
                            clientMinecraftStream.WritePacket(titleDisplayTime);
                            Thread.Sleep(50);
                            // send title
                            PacketBuffer title = new(clientMinecraftStream.protocolVersion);
                            title.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x45 : 0x48);
                            title.WriteVarInt(0);
                            title.WriteString("{\"text\":\"§dConnecting...\"}");
                            clientMinecraftStream.WritePacket(title);
                            Thread.Sleep(50);
                            new Thread(() =>
                            {
                                try
                                {
                                    while (sendKeepAlive)
                                    {
                                        PacketBuffer keepAlive = new(clientMinecraftStream.protocolVersion);
                                        keepAlive.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x00 : 0x1f);
                                        if (clientMinecraftStream.protocolVersion == 47)
                                        {
                                            keepAlive.WriteVarInt(0);
                                        }
                                        else
                                        {
                                            keepAlive.WriteLong(DateTimeOffset.Now.ToUnixTimeMilliseconds());
                                        }
                                        Thread.Sleep(50);
                                    }
                                } catch { /*ignored*/ }
                            }).Start();
                        }

                        void SendMessageToClient(string msg)
                        {
                            PacketBuffer _buffer = new(0);
                            _buffer.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x02 : 0x0f);
                            JsonObject obj = new();
                            obj["text"] = "§7§o[§b§oAuto§d§oMyth§a§oTunnel§r§7§o] " + msg;
                            _buffer.WriteString(obj.ToString());
                            _buffer.WriteByte(0);
                            clientMinecraftStream.WritePacket(_buffer);
                        }

                        if (proxySettings != null)
                        {
                            ProxyClient<Socks5> proxyClient = new()
                            {
                                Settings = proxySettings
                            };
                            serverStream = proxyClient.GetDestinationStream(targetServer, targetPort);
                        }
                        else
                        {
                            // serverStream = new TcpClient("localhost", 25566).GetStream();
                            serverStream = new TcpClient(targetServer, targetPort).GetStream();
                        }
                        MinecraftStream serverMinecraftStream = new(serverStream, clientMinecraftStream.protocolVersion);
                        if (supported) SendMessageToClient("Handshaking...");
                        // handshake
                        PacketBuffer handshake = new(clientMinecraftStream.protocolVersion);
                        handshake.WriteVarInt(0);
                        handshake.WriteVarInt(clientMinecraftStream.protocolVersion);
                        handshake.WriteString(rewriteServerAddress ? "mc.hypixel.net" : targetServer);
                        handshake.WriteUShort(25565);
                        handshake.WriteVarInt(2);
                        serverMinecraftStream.WritePacket(handshake);
                        OnJoin?.Invoke("");
                        if (supported) SendMessageToClient("Login...");
                        // login start
                        PacketBuffer loginStartPacket = new(clientMinecraftStream.protocolVersion);
                        loginStartPacket.WriteVarInt(0);
                        loginStartPacket.WriteString(_profile!.Name);
                        // loginStartPacket.WriteUuid(Uuid.Parse(_profile.Uuid));
                        serverMinecraftStream.WritePacket(loginStartPacket);
                        PacketBuffer encryptionRequest = serverMinecraftStream.ReadPacket();
                        packetId = encryptionRequest.ReadVarInt();
                        if (packetId != 1)
                        {
                            AnsiConsole.MarkupLine("Warn: Invalid packet received from server. packetId=" + packetId);
                            if (supported && packetId == 0)
                            {
                                AnsiConsole.MarkupLine("Warn: Disconnect packet received.");
                                PacketBuffer kickPacket = new(clientMinecraftStream.protocolVersion);
                                kickPacket.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x40 : 0x1a);
                                kickPacket.WriteString(encryptionRequest.ReadString());
                                clientMinecraftStream.WritePacket(kickPacket);
                                sendKeepAlive = false;
                            }
                            else clientMinecraftStream.WritePacket(encryptionRequest);
                            serverStream.Dispose();
                            client.Dispose();
                            return;
                        }
                        string serverId = encryptionRequest.ReadString();
                        byte[] publicKey = encryptionRequest.ReadBytes(encryptionRequest.ReadVarInt());
                        byte[] verifyToken = encryptionRequest.ReadBytes(encryptionRequest.ReadVarInt());
                        Aes aes = Aes.Create();
                        aes.KeySize = 128;
                        aes.GenerateKey();

                        string hex = EncryptionHelper.ComputeHash(serverId, aes.Key, publicKey);

                        MicrosoftApi.RequireJoinServer(hex, _profile!.AccessToken, _profile!.Uuid.Replace("-", ""));
                        RSA? rsa = EncryptionHelper.DecodePublicKey(publicKey);
                        if (rsa == null)
                        {
                            throw new NullReferenceException("failed to decode public key");
                        }
                        byte[] sharedSecret = rsa.Encrypt(aes.Key, RSAEncryptionPadding.Pkcs1);
                        byte[] encVerToken = rsa.Encrypt(verifyToken, RSAEncryptionPadding.Pkcs1);
                        PacketBuffer encryptionResponse = new(clientMinecraftStream.protocolVersion);
                        encryptionResponse.WriteVarInt(1);
                        encryptionResponse.WriteVarInt(sharedSecret.Length);
                        encryptionResponse.WriteBytes(sharedSecret);
                        encryptionResponse.WriteVarInt(encVerToken.Length);
                        encryptionResponse.WriteBytes(encVerToken);
                        serverMinecraftStream.WritePacket(encryptionResponse);
                        serverMinecraftStream.EnableEncryption(aes.Key);
                        
                        while (true)
                        {
                            // handle next packet to pass the login process
                            PacketBuffer nextPacket = serverMinecraftStream.ReadPacket();
                            int nextPacketId = nextPacket.ReadVarInt();
                            if (nextPacketId == 2)
                            {
                                if (supported) {
                                    SendMessageToClient("Joining...");
                                    Thread.Sleep(1500);
                                    // send respawn packet
                                    PacketBuffer respawnPacket = new(clientMinecraftStream.protocolVersion);
                                    respawnPacket.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x07 : 0x35);
                                    respawnPacket.WriteInt(0);
                                    respawnPacket.WriteByte(0);
                                    respawnPacket.WriteByte(0);
                                    respawnPacket.WriteString("default");
                                    clientMinecraftStream.WritePacket(respawnPacket);
                                }
                                else
                                {
                                    clientMinecraftStream.WritePacket(new PacketBuffer(nextPacket.GetBuffer(), clientMinecraftStream.protocolVersion));
                                }
                                break;
                            }
                            else if (nextPacketId == 0)
                            {
                                AnsiConsole.MarkupLine("Warn: Disconnect packet received.");
                                if (supported) {
                                    PacketBuffer kickPacket = new(clientMinecraftStream.protocolVersion);
                                    kickPacket.WriteVarInt(clientMinecraftStream.protocolVersion == 47 ? 0x40 : 0x1a);
                                    kickPacket.WriteString(nextPacket.ReadString());
                                    clientMinecraftStream.WritePacket(kickPacket);
                                    sendKeepAlive = false;
                                }
                                else
                                {
                                    clientMinecraftStream.WritePacket(new PacketBuffer(nextPacket.GetBuffer(), clientMinecraftStream.protocolVersion));
                                }
                                serverStream.Dispose();
                                client.Dispose();
                                return;
                            }
                            else if (nextPacketId == 3)
                            {
                                compressionThreshold = nextPacket.ReadVarInt();
                                serverMinecraftStream.SetCompressionThreshold(compressionThreshold);
                                SendMessageToClient("Set compression threshold to " + compressionThreshold);
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[bold yellow]Warn: Invalid packet received from server. packetId={nextPacketId}[/]");
                                serverStream.Dispose();
                                client.Dispose();
                                return;
                            }
                        }
                        sendKeepAlive = false;
                        AnsiConsole.MarkupLine($"[bold cyan]Start transfer data for client {client.Client.RemoteEndPoint}.[/]");
                        using (serverStream)
                        using (clientStream)
                        {
                            ExchangeStreams(serverMinecraftStream, clientMinecraftStream);
                        }
                        return;
                    default:
                        client.Dispose();
                        return;
                }
            }
            // AnsiConsole.MarkupLine($"[yellow]Warn: Client {client.Client.RemoteEndPoint} try to join server but proxy is not implemented yet.[/]");
        } catch (Exception e) {
            AnsiConsole.MarkupLine($"[red]Error: {e.Message.Replace("[", "(").Replace("]", ")")}[/]");
            AnsiConsole.MarkupLine($"[red]{e.StackTrace}[/]");
            try
            {
                client.Dispose();
            }
            catch { /*ignored*/ }
        }
    }
    
    public void ExchangeStreams(MinecraftStream stream1, MinecraftStream stream2)
    {
        try
        {
            PacketQueue c2sQueue = new();
            PacketQueue s2cQueue = new();
            Task task1 = Task.Run(() => CopyStream(stream1, stream2, s2cQueue));
            Task task2 = Task.Run(() => CopyStream(stream2, stream1, c2sQueue));
            if (ProtocolVersion.SUPPORTED_VERSIONS.Contains(stream1.protocolVersion)) {
                PacketProcessor processor = new(c2sQueue, s2cQueue, stream2, stream1, null, compressionThreshold, injectCommand: injectCommand);
                Task.Run(() => processor.Start());
            }
            else
            {
                AnsiConsole.MarkupLine("[bold yellow]Warn: Protocol version is not supported, packet processor is not started.[/]");
            }
            Task.WaitAll(task1, task2);
        }
        catch
        {
            // 忽略异常
        }
    }

    private static void CopyStream(MinecraftStream source, MinecraftStream destination, PacketQueue queue)
    {
        try
        {
            while (true)
            {
                PacketBuffer _buffer = source.ReadPacket();
                destination.WritePacket(_buffer);
                queue.Enqueue(_buffer);
            }
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[red]Error: {e.Message}[/]");
            AnsiConsole.MarkupLine($"[red]{e.StackTrace}[/]");
        }
    }
}