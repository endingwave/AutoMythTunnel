using System.Text.Json.Nodes;
using AutoMythTunnel.Data;
using AutoMythTunnel.Exceptions;
using AutoMythTunnel.Utils;
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace AutoMythTunnel;

public class MythApi
{
    private static string _baseUrl = "https://myth.galcraft.top/api";
    public static readonly string[] ServerTypes = ["CtccServer", "CmccServer", "CuccServer"];
    public static UserInfo? UserInfo { get; private set; }
    
    public static UserInfo Login(string username, string password)
    {
        string client = "SPARK";
        string device = "WEBSITE";
        long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        string passwordMd5 = StringUtils.GetMd5($"sa-spark{StringUtils.GetMd5(password)}");
        string sign = StringUtils.GetMd5(client + device + username + passwordMd5 + timestamp);
        string url = $"{_baseUrl}/Sign/doLogin?key={username}&password={sign}&device={device}&client={client}&time={timestamp}";
        JsonObject response = HttpUtils.GetJson(url);
        if (response["code"].GetValue<int>() != 200)
        {
            throw new ApiFailedException(response["msg"].GetValue<string>());
        }
        string tokenName = response["data"]["tokenInfo"]["tokenName"].GetValue<string>();
        string tokenValue = response["data"]["tokenInfo"]["tokenValue"].GetValue<string>();
        UserInfo = new UserInfo
        {
            Username = username,
            Password = password,
            Role = response["data"]["userInfo"]["roleName"].GetValue<string>(),
            TokenName = tokenName,
            TokenValue = tokenValue
        };
        return UserInfo;
    }
    
    public static UserInfo Login(UserInfo userInfo)
    {
        string url = $"{_baseUrl}/Sign/getLoginInfo";
        JsonObject response = HttpUtils.GetJson(url, new Dictionary<string, string> { { userInfo.TokenName, userInfo.TokenValue } });
        if (response["code"].GetValue<int>() != 200)
        {
            return userInfo.ReLogin();
        }
        else
        {
            UserInfo = userInfo;
            return userInfo;
        }
    }

    public static AccountInfo[] GetAccounts()
    {
        if (UserInfo == null) throw new NotLoggedException();

        string url = $"{_baseUrl}/JSIP/Proxy/Accounts";
        JsonObject response = HttpUtils.GetJson(url, new Dictionary<string, string> { { UserInfo.TokenName, UserInfo.TokenValue } });

        AccountInfo[] accounts = response["data"].AsArray().Select(account => new AccountInfo
        {
            Uuid = account["Uuid"].GetValue<string>(),
            Username = account["Username"].GetValue<string>(),
            Password = account["Password"].GetValue<string>(),
            Attach = account["Attach"].GetValue<string>(),
            Type = account["Type"].GetValue<string>(),
            User = account["User"].GetValue<string>(),
            IsHypixel21 = account["IsHypixel21"].GetValue<bool>(),
            CreateDate = account["CreateDate"].GetValue<DateTime>(),
            Email = account["Email"].GetValue<string>(),
            Oa2ClientId = account["Oa2ClientId"].GetValue<string>(),
            Oa2ExpireDate = account["Oa2ExpireDate"].GetValue<DateTime>()
        }).ToArray();

        return accounts;
    }

    public static bool DeleteAccount(string uuid)
    {
        string url = $"{_baseUrl}/JSIP/Proxy/Account?uuid={uuid}";
        JsonObject response = HttpUtils.DeleteJson(url, new Dictionary<string, string> { { UserInfo.TokenName, UserInfo.TokenValue } });
        return response["code"].GetValue<int>() == 0;
    }

    public static bool AddAccount(string name)
    {
        string url = $"{_baseUrl}/JSIP/Proxy/Account";
        JsonObject requestBody = new JsonObject
        {
            ["Attach"] = "{}",
            ["Email"] = "",
            ["Oa2ClientId"] = "54473e32-df8f-42e9-a649-9419b0dab9d3",
            ["Oa2RefreshToken"] = "",
            ["Password"] = "",
            ["Type"] = "Mojang",
            ["Username"] = name
        };
        JsonObject response = HttpUtils.PostJson(url, requestBody, new Dictionary<string, string> { { UserInfo.TokenName, UserInfo.TokenValue } });
        if (response["code"].GetValue<int>() != 0)
        {
            throw new ApiFailedException($"Failed to add account [code: {response["code"].GetValue<int>()}] {response["msg"].GetValue<string>()}");
        }
        return true;
    }
    
    public static ServerInfo? GetServer()
    {
        string url = $"{_baseUrl}/JSIP/Proxy/Query";
        JsonObject response = HttpUtils.GetJson(url, new Dictionary<string, string> { { UserInfo.TokenName, UserInfo.TokenValue } });
        if (response["code"].GetValue<int>() != 0)
        {
            return null;
        }
        JsonObject data = response["data"].AsObject();
        return new ServerInfo
        {
            Id = data["Id"].GetValue<string>(),
            IP = data["IP"].GetValue<string>(),
            City = data["City"].GetValue<string>(),
            Status = data["Status"].GetValue<string>(),
            IsLost = data["IsLost"].GetValue<bool>(),
            GameServerAddress = data["GameServerAddress"].GetValue<string>(),
            ExpireDate = data["ExpireDate"].GetValue<DateTime>()
        };
    }
    
    public static bool DeleteServer(string id)
    {
        string url = $"{_baseUrl}/JSIP/Proxy/Change/{id}";
        JsonObject response = HttpUtils.GetJson(url, new Dictionary<string, string> { { UserInfo.TokenName, UserInfo.TokenValue } });
        return response["msg"].GetValue<string>().Contains("已销毁当前服务器");
    }
    
    public static bool RenewServer(string id)
    {
        string url = $"{_baseUrl}/JSIP/Proxy/Renew/{id}/60";
        JsonObject response = HttpUtils.GetJson(url, new Dictionary<string, string> { { UserInfo.TokenName, UserInfo.TokenValue } });
        return response["code"].GetValue<int>() == 0;
    }
}