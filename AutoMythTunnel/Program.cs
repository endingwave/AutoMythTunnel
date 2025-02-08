// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Reflection;
using System.Text.Json;
using AutoMythTunnel.Bot;
using AutoMythTunnel.Data;
using AutoMythTunnel.Proxy;
using AutoMythTunnel.Utils;
using MineSharp.Bot;
using MineSharp.Core.Common;
using MineSharp.Core.Serialization;
using SocksSharp.Proxy;
using Spectre.Console;

namespace AutoMythTunnel;

internal static class Program
{
    private static readonly string defaultProxyConfigContent =
        "{\n\t\"UseProxy\": false, \n\t\"ProxyHost\": \"\", \n\t\"ProxyPort\": 0\n}";
    
    public static void Main(string[] args)
    {
        try
        {
            if (!File.Exists("ProxyConfig.json"))
            {
                File.WriteAllText("ProxyConfig.json", defaultProxyConfigContent);
                AnsiConsole.MarkupLine("[bold yellow]ProxyConfig.json[/] [bold green]配置文件已创建, 编辑后按下回车键[/]");
                AnsiConsole.MarkupLine("[bold cyan]该文件用于卡加速IP时配置代理服务器, 用于解决部分地区无法正常直连Hypixel的问题, 请根据需要修改以下内容:[/]");
                AnsiConsole.MarkupLine("[bold yellow]ProxyHost[/] [bold green]代理服务器地址[/]");
                AnsiConsole.MarkupLine("[bold yellow]ProxyPort[/] [bold green]代理服务器端口[/]");
                AnsiConsole.MarkupLine("[bold yellow]UseProxy[/] [bold green]是否使用代理[/]");
                Console.ReadLine();
            }

            // read config
            ProxyConfig proxyConfig = JsonSerializer.Deserialize<ProxyConfig>(File.ReadAllText("ProxyConfig.json"))!;
            if (File.Exists("MythUserInfo.json"))
            {
                UserInfo? userInfo = JsonSerializer.Deserialize<UserInfo>(File.ReadAllText("MythUserInfo.json"));
                if (userInfo != null) MythApi.Login(userInfo);
                else AskLogin();
            }
            else AskLogin();

            File.WriteAllText("MythUserInfo.json", JsonSerializer.Serialize(MythApi.UserInfo));
            AnsiConsole.MarkupLine(
                $"[bold green]登录成功[/] [bold cyan]欢迎回来! {StringUtils.HideSensitiveInfo(MythApi.UserInfo!.Username)}[/]");

            string mode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold purple]请选择加速模式[/]")
                    .PageSize(4)
                    .AddChoices("获取IP", "卡加速IP", "NaProxy-DE-自动改名")
            );
            AnsiConsole.MarkupLine($"[bold cyan]模式选择:[/] [bold underline yellow]{mode}[/]");
            if (mode == "NaProxy-DE-自动改名")
            {
                string ip = AnsiConsole.Ask<string>("请输入IP");
                string[] splitIpPort2 = ip.Split(":");
                ProxyServer proxyServer2 = new("null", null, splitIpPort2[0],
                    splitIpPort2.Length == 2 ? int.Parse(splitIpPort2[1]) : 25565, offline: true);
                proxyServer2.Start();
                AnsiConsole.MarkupLine("[bold green]开启代理成功, 请尽快加入服务器 localhost.[/]");
                return;
            }
            bool loginNew = true;
            string refreshToken = "";
            if (File.Exists("RefreshToken.txt"))
            {
                loginNew = AnsiConsole.Confirm("是否登录新的Minecraft账号?", false) /*false*/;
            }

            if (loginNew)
            {
                AnsiConsole.MarkupLine("[bold cyan]请在浏览器打开以下链接并登录Minecraft账户:[/] " +
                                       "[bold underline yellow]https://login.live.com/oauth20_authorize.srf?client_id=54473e32-df8f-42e9-a649-9419b0dab9d3&scope=XboxLive.signin+offline_access+openid+email" +
                                       "&redirect_uri=https%3a%2f%2fmccteam.github.io%2fredirect.html&response_type=code&response_mode=fragment&prompt=select_account[/]");
                string code = LoginWithMicrosoft();
                AnsiConsole.MarkupLine($"[bold green]开始Microsoft登录[/]");
                // get refresh token
                refreshToken = MicrosoftApi.GetRefreshToken(code);
                AnsiConsole.MarkupLine($"[bold green]Refresh Token获取成功[/]");
            }
            else
            {
                refreshToken = File.ReadAllText("RefreshToken.txt");
            }

            // get ms access token
            (string msAccessToken, refreshToken) = MicrosoftApi.GetMsAccessToken(refreshToken);
            File.WriteAllText("RefreshToken.txt", refreshToken);
            AnsiConsole.MarkupLine("[bold green]Microsoft Access Token获取成功[/]");
            // authenticate with XBL
            (string xblToken, string userHash) = MicrosoftApi.AuthenticateWithXBL(msAccessToken);
            AnsiConsole.MarkupLine("[bold green]XBL Token获取成功[/]");
            // authenticate with XSTS
            string xstsToken = MicrosoftApi.AuthenticateWithXSTS(xblToken);
            AnsiConsole.MarkupLine("[bold green]XSTS Token获取成功[/]");
            // get the minecraft access token
            string mcAccessToken = MicrosoftApi.GetMinecraftAccessToken(xstsToken, userHash);
            AnsiConsole.MarkupLine("[bold green]Minecraft Access Token获取成功[/]");
            MinecraftProfile profile = MicrosoftApi.GetMcProfile(mcAccessToken);
            ProxySettings? proxySettings = null;
            if (proxyConfig.UseProxy)
            {
                proxySettings = new ProxySettings
                {
                    Host = proxyConfig.ProxyHost,
                    Port = proxyConfig.ProxyPort,
                };
            }

            switch (mode)
            {
                case "获取IP":
                    CheckAccounts(profile!);
                    ServerInfo? infos = MythApi.GetExistedServer();
                    if (infos != null && AnsiConsole.Confirm("是否销毁先前的服务器?"))
                    {
                        infos.Delete();
                    }

                    string status = MythApi.GetNewServer();
                    AnsiConsole.MarkupLine($"[bold cyan]开始获取IP: {status}[/]");
                    while (!status.Contains("仍有可用服务器"))
                    {
                        try
                        {
                            status = MythApi.GetNewServer();
                            AnsiConsole.MarkupLine($"[bold cyan]获取状态: {status}[/]");
                        }
                        catch
                        {
                            /*ignored*/
                        }
                    }

                    ServerInfo info = MythApi.GetExistedServer() ?? throw new Exception("获取服务器信息失败");
                    AnsiConsole.MarkupLine(
                        $"[bold green]获取成功[/] [bold cyan]Id: {StringUtils.HideSensitiveInfo(info.Id)}[/]");
                    bool useMyth2 = AnsiConsole.Confirm("是否使用自动测速?");
                    string fastestEnterance = useMyth2 ? GetBestEnteranceIp().Result : AnsiConsole.Ask<string>("请输入IP");
                    AnsiConsole.MarkupLine("[bold cyan]正在创建代理...[/]");
                    string[] splitIpPort = fastestEnterance.Split(':');
                    ProxyServer proxyServer = new(mcAccessToken, null, splitIpPort[0],
                        splitIpPort.Length == 2 ? int.Parse(splitIpPort[1]) : 25565, injectCommand: "/lobby sw");
                    proxyServer.Start();
                    AnsiConsole.MarkupLine("[bold green]加速成功, 请尽快加入服务器 localhost.[/]");
                    AutoRenew();
                    break;
                case "卡加速IP":
                    bool useMyth = AnsiConsole.Confirm("是否使用Myth提供的入口IP?");
                    if (useMyth) CheckAccounts(profile!, true);
                    string ip = useMyth ? GetBestEnteranceIp().Result : AnsiConsole.Ask<string>("请输入IP");
                    AnsiConsole.MarkupLine("[bold cyan]正在卡加速IP...[/]");
                    ProxyServer proxyServer1 = new(mcAccessToken, proxySettings, localPort: 45399);
                    proxyServer1.Start();
                    MineSharpBot bot = HypixelPlayerBot.Start();
                    bot.Disconnect().Task.Wait();
                    proxyServer1.Stop();
                    string[] splitIpPort2 = ip.Split(":");
                    ProxyServer proxyServer2 = new(mcAccessToken, null, splitIpPort2[0],
                        splitIpPort2.Length == 2 ? int.Parse(splitIpPort2[1]) : 25565, injectCommand: "/lobby sw");
                    proxyServer2.Start();
                    AnsiConsole.MarkupLine("[bold green]加速成功, 请尽快加入服务器 localhost.[/]");
                    break;
                default:
                    AnsiConsole.MarkupLine("[bold red]未知模式[/]");
                    AnsiConsole.MarkupLine($"[bold red]按下任意键退出[/]");
                    Console.ReadKey();
                    Environment.Exit(1);
                    break;
            }

            while (true)
            {
                Thread.Sleep(100);
            }
        } catch (Exception e) {
            AnsiConsole.MarkupLine($"[bold red]Error: {e.ToString().Replace("[", "(").Replace("]", ")")}[/]");
            AnsiConsole.MarkupLine($"[bold red]按下任意键退出[/]");
            Console.ReadKey();
            Environment.Exit(1);
        }
    }

    private static void AskLogin()
    {
        ConsoleUtils.WriteWithColor("用户名: ", ConsoleColor.Yellow);
        string username = ConsoleUtils.ReadPassword();
        ConsoleUtils.WriteWithColor("密码: ", ConsoleColor.Yellow);
        string password = ConsoleUtils.ReadPassword();
        MythApi.Login(username, password);
    }

    private static async Task<string> GetBestEnteranceIp()
    {
        AnsiConsole.MarkupLine("[bold cyan]正在获取所有可用的入口IP[/]");
        List<string> enteranceIp = new();
        foreach (EnteranceServerInfo enteranceServerInfo in MythApi.GetEnteranceServers())
        {
            enteranceIp.Add(enteranceServerInfo.CmccServer ?? "0.0.0.0");
            enteranceIp.Add(enteranceServerInfo.CtccServer ?? "0.0.0.0");
            enteranceIp.Add(enteranceServerInfo.CuccServer ?? "0.0.0.0");
        }
        AnsiConsole.MarkupLine("[bold green]获取成功[/]");
        AnsiConsole.MarkupLine("[bold cyan]正在测速...[/]");

        string? fastestEnterance = null;
        double fastestTime = double.MaxValue;
        List<Task<(string, double)>> pingTasks = new();

        foreach (string eIp in enteranceIp)
        {
            pingTasks.Add(Task.Run(() =>
            {
                try
                {
                    string[] ipPort = eIp.Split(':');
                    string ip = ipPort[0];
                    int port = ipPort.Length == 2 ? int.Parse(ipPort[1]) : 25565;
                    double ping = NetworkUtils.Tcping(ip, port);
                    return (eIp, ping);
                }
                catch
                {
                    return (eIp, double.MaxValue);
                }
            }));
        }

        (string, double)[] results = await Task.WhenAll(pingTasks);

        foreach ((string eIp, double ping) in results)
        {
            if (ping < fastestTime)
            {
                fastestTime = ping;
                fastestEnterance = eIp;
            }
        }

        if (fastestEnterance == null)
        {
            AnsiConsole.MarkupLine("[bold red]测速失败[/]");
            AnsiConsole.MarkupLine($"[bold red]按下任意键退出[/]");
            Console.ReadKey();
            Environment.Exit(1);
        }

        AnsiConsole.MarkupLine($"[bold green]测速完成[/] [bold cyan]最快的入口IP: {StringUtils.HideSensitiveInfo(fastestEnterance)}[/]");
        return fastestEnterance;
    }

    private static void AutoRenew()
    {
        while (true)
        {
            try {
                ServerInfo? info = MythApi.GetExistedServer();
                if (info == null)
                {
                    AnsiConsole.MarkupLine("[bold red]服务器已被销毁[/]");
                    Environment.Exit(0);
                }

                if (info.ExpireDate > DateTime.Now.AddMinutes(5))
                {
                    Thread.Sleep(10000);
                    continue;
                }
                info.Renew();
                AnsiConsole.MarkupLine("[bold green]续期成功[/]");
            } catch {}
        }
    }

    private static void CheckAccounts(MinecraftProfile profile, bool forceJoin = false)
    {
        AccountInfo[] accounts = MythApi.GetAccounts();
        if (accounts.Length >= 3 &&
            accounts.FirstOrDefault((accountInfo => accountInfo.Username == profile.Name)) == null)
        {
            AnsiConsole.MarkupLine("[bold cyan]账户列表已满, 正在清理...[/]");
            foreach (AccountInfo accountInfo in accounts)
            {
                accountInfo.Delete();
            }

            AnsiConsole.MarkupLine("[bold cyan]账户列表已清理[/]");
        }

        if (accounts.FirstOrDefault((accountInfo => accountInfo.Username == profile.Name)) != null)
        {
            AnsiConsole.MarkupLine("[bold cyan]账户已存在, 正在重新加入...[/]");
            accounts.FirstOrDefault((accountInfo => accountInfo.Username == profile.Name))?.Delete();
            MythApi.AddAccount(profile.Name, forceJoin);
            return;
        }
        MythApi.AddAccount(profile.Name, forceJoin);
        AnsiConsole.MarkupLine("[bold green]账户添加成功[/]");
    }
    
    private static string LoginWithMicrosoft()
    {
        Console.Write("Input the code:");
        return Console.ReadLine() ?? throw new Exception("Code is null");
    }
}

internal class ProxyConfig
{
    public bool UseProxy { get; set; }
    public string ProxyHost { get; set; }
    public int ProxyPort { get; set; }
}