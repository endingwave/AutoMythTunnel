using AutoMythTunnel.Utils;
using MineSharp.Bot;
using MineSharp.Bot.Plugins;
using MineSharp.Data;
using Spectre.Console;

namespace AutoMythTunnel.Bot;

public static class HypixelPlayerBot
{
    public static MineSharpBot Start()
    {
        MineSharpBot bot = new BotBuilder()
            .OfflineSession("MythTunnelTest")
            .Host("127.0.0.1").Port(45399)
            .Data("1.18.2")
            .CreateAsync().Result;
        bool running = true;
        bool isNotFirst = false;
        
        PhysicsPlugin physics = bot.GetPlugin<PhysicsPlugin>();
        ChatPlugin chat = bot.GetPlugin<ChatPlugin>();
        chat.OnChatMessageReceived += (_, _, chatMessage, _) =>
        {
            if (chatMessage.GetMessage(null).Contains("{\"bold\":true,\"color\":\"yellow\",\"text\":\"Protect your bed and destroy the enemy beds.\"}") || chatMessage.GetMessage(null).Contains("Respawning in"))
            {
                running = false;
            }
            if (chatMessage.GetMessage(null).Contains("/limbo for more information."))
            {
                chat.SendChat("/rej").Wait();
            }
            Console.WriteLine(chatMessage.GetMessage(null));
            if (isNotFirst) return;
            isNotFirst = true;
            AnsiConsole.MarkupLine("[bold cyan]已加入服务器[/]");
            chat.SendChat("/lang English").Wait();
            Thread.Sleep(1000);
            chat.SendChat("/play bedwars_eight_one").Wait();
        };
        bot.Connect();
        while (running)
        {
            Thread.Sleep(100);
        }

        return bot;
    }
}