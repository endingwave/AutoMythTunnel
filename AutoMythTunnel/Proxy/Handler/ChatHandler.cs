using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AutoMythTunnel.Proxy.Sniffer;
using AutoMythTunnel.Utils;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.SoundOut;
using MineSharp.Core.Common;
using MineSharp.Core.Serialization;
using Spectre.Console;

namespace AutoMythTunnel.Proxy.Handler;

public class ChatHandler
{
    private PacketBuffer? lastPlayBuffer = null;
    private bool IsFirstMessage = true;
    private int killCount;
    private int lagCount;
    private float health = 20;
    private int selfEntityID = 0;
    private int lastAttackEntityID = 0;
    private double lastAttackEntityHealth = 0;
    private long lastAttackTime = 0;
    private Dictionary<int, double> entityMaxHealth = new();
    private Dictionary<int, double> entityHealth = new();
    private readonly string? injectCommand;
    private static Assembly assembly = Assembly.GetExecutingAssembly();
    private static Dictionary<string, WaveFileReader> waveFileReaders = new();
    public ProtocolVersion protocolVersion;

    public ChatHandler(string? injectCommand = null)
    {
        this.injectCommand = injectCommand;
        PreloadSoundFromResourceDir("win.wav");
        PreloadSoundFromResourceDir("hit.wav");
        PreloadSoundFromResourceDir("lose.wav");
        PreloadSoundFromResourceDir("kill.wav");
        PreloadSoundFromResourceDir("2kill.wav");
        PreloadSoundFromResourceDir("3kill.wav");
        PreloadSoundFromResourceDir("4kill.wav");
        PreloadSoundFromResourceDir("5kill.wav");
        PreloadSoundFromResourceDir("morekill.wav");
    }
    
    public static void PreloadSoundFromResourceDir(string resourceName)
    {
        try {
            string resourcePath = Path.Combine(Path.GetDirectoryName(assembly.Location), "Resources", resourceName);
            waveFileReaders[resourceName] = new(resourcePath);
        } catch (Exception e) {
            AnsiConsole.MarkupLine($"[bold red]Error: {e.Message}[/]");
        }
    }
    
    private static int PlaySoundFromResourceDir(string resourceName, PacketProcessor processor)
    {
        try {
            new Task(() =>
            {
                WasapiOut wasapiOut = new();
                WaveFileReader waveFileReader = waveFileReaders[resourceName];
                waveFileReader.Position = 0;
                wasapiOut.Initialize(waveFileReader);
                wasapiOut.Play();
                wasapiOut.WaitForStopped();
                wasapiOut.Dispose();
            }).Start();
            return 0;
        } catch (Exception e) {
            AnsiConsole.MarkupLine($"[bold red]Error: {e.Message}[/]");
            return -1;
        }
    }
    
    public void OnPacket(EnumPacketType enumPacketId, PacketBuffer buffer, PacketProcessor processor)
    {
        switch (enumPacketId)
        {
            case EnumPacketType.CLIENT_CHAT:
                string message = buffer.ReadString();
                if (message is "/req" or "/requeue" && lastPlayBuffer != null)
                {
                    processor.WriteToServer(lastPlayBuffer);
                }
                if (!message.StartsWith("/play")) return;
                lastPlayBuffer = new(buffer.GetBuffer(), processor.clientStream.protocolVersion);
                AnsiConsole.MarkupLine("[bold green]Play command received![/]");
                break;
            case EnumPacketType.SERVER_JOIN_GAME:
                selfEntityID = buffer.ReadInt();
                AnsiConsole.MarkupLine($"[bold green]Self entity ID: {selfEntityID}[/]");
                break;
            case EnumPacketType.SERVER_SET_POSITION_AND_ROTATION:
                lagCount++;
                SendMessageToClient($"Lagback received (x{lagCount})", processor);
                break;
            case EnumPacketType.SERVER_RESPAWN:
                lagCount = 0;
                killCount = 0;
                health = 20;
                break;
            case EnumPacketType.SERVER_HEALTH_CHANGE:
            {
                float newHealth = buffer.ReadFloat();
                if (Math.Abs(newHealth - health) > 0.1)
                {
                    string healthChange = newHealth > health ? "§a§o+" : "§c§o-";
                    SendMessageToClient($"Health: {healthChange}{Math.Round(Math.Abs(newHealth - health), 1)}{(Math.Abs(newHealth - health) % 1 == 0 ? ".0" : "")} §7§o({Math.Round(newHealth, 1)}{(newHealth % 1 == 0 ? ".0)" : ")")}", processor);
                    health = newHealth;
                }
                break;
            }
            case EnumPacketType.SERVER_CHAT:
                try {
                    if (IsFirstMessage)
                    {
                        PacketBuffer playCommand = new(processor.clientStream.protocolVersion);
                        playCommand.WriteVarInt(protocolVersion.ParsePacketId(EnumPacketType.CLIENT_CHAT));
                        playCommand.WriteString("/lang English");
                        processor.WriteToServer(playCommand);
                        if (injectCommand != null)
                        {
                            playCommand = new(processor.clientStream.protocolVersion);
                            playCommand.WriteVarInt(protocolVersion.ParsePacketId(EnumPacketType.CLIENT_CHAT));
                            playCommand.WriteString(injectCommand);
                            processor.WriteToServer(playCommand);
                        }
                        AnsiConsole.MarkupLine("[bold green]Command sent![/]");
                        IsFirstMessage = false;
                    }
                    JsonObject message2 = JsonObject.Parse(buffer.ReadString())!.AsObject();
                    int messagePosition = buffer.ReadByte();
                    if (messagePosition == 2) return;
                    string rawMessage = message2["text"]?.GetValue<string>() ?? "";
                    try {
                        foreach (JsonNode? jsonNode in message2["extra"]?.AsArray())
                        {
                            if (jsonNode is not JsonObject obj) continue;
                            rawMessage += obj["text"]?.GetValue<string>() ?? "";
                        }
                    } catch {}
                    if (rawMessage.Contains("You won!"))
                    {
                        PlaySoundFromResourceDir("win.wav", processor);
                        killCount = 0;
                    }

                    if (rawMessage.Contains("You died!"))
                    {
                        PlaySoundFromResourceDir("lose.wav", processor);
                        killCount = 0;
                        SendMessageToClient("欸你怎么死了", processor);
                    }

                    if (rawMessage.Contains("SkyWars Experience (Kill)"))
                    {
                        killCount++;
                        string killSound = killCount switch
                        {
                            1 => "kill.wav",
                            2 => "2kill.wav",
                            3 => "3kill.wav",
                            4 => "4kill.wav",
                            5 => "5kill.wav",
                            _ => "morekill.wav"
                        };
                        SendMessageToClient("Kill count: " + killCount, processor);
                        PlaySoundFromResourceDir(killSound, processor);
                    }
                    Console.WriteLine(rawMessage);
                } catch (Exception e) {
                    AnsiConsole.MarkupLine($"[bold red]Error: {e.Message}[/]");
                }

                break;
        }
    }
    
    public void SendMessageToClient(string message, PacketProcessor processor)
    {
        PacketBuffer buffer = new(0);
        buffer.WriteVarInt(protocolVersion.ParsePacketId(EnumPacketType.SERVER_CHAT));
        JsonObject obj = new();
        obj["text"] = "§7§o[§b§oAuto§d§oMyth§a§oTunnel§r§7§o] " + message;
        buffer.WriteString(obj.ToString());
        buffer.WriteByte(0);
        processor.WriteToClient(buffer);
    }
}