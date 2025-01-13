// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text.Json;
using AutoMythTunnel.Data;
using AutoMythTunnel.Utils;

namespace AutoMythTunnel;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (File.Exists("UserInfo.json"))
        {
            UserInfo? userInfo = JsonSerializer.Deserialize<UserInfo>(File.ReadAllText("UserInfo.json"));
            if (userInfo != null) MythApi.Login(userInfo); else AskLogin();
        }
        else AskLogin();
        File.WriteAllText("UserInfo.json", JsonSerializer.Serialize(MythApi.UserInfo));
        ConsoleUtils.WriteLineWithColor($"登录成功, 欢迎回来! {MythApi.UserInfo!.Role} {StringUtils.HideSensitiveInfo(MythApi.UserInfo!.Username)}", ConsoleColor.Green);
        ConsoleUtils.WriteWithColor("复制 ZBProxy 可执行文件...", ConsoleColor.Cyan);
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoMythTunnel.Resources.zbp.exe")!;
        using FileStream fileStream = new("zbp.exe", FileMode.Create);
        stream.CopyTo(fileStream);
    }

    private static void AskLogin()
    {
        ConsoleUtils.WriteWithColor("用户名: ", ConsoleColor.Yellow);
        string username = ConsoleUtils.ReadPassword();
        ConsoleUtils.WriteWithColor("密码: ", ConsoleColor.Yellow);
        string password = ConsoleUtils.ReadPassword();
        MythApi.Login(username, password);
    }
}