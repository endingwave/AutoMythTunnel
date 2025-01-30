using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using AutoMythTunnel.Data;
using Newtonsoft.Json.Linq;

namespace AutoMythTunnel;

public class MicrosoftApi
{
    public static string GetRefreshToken(string code)
    {
        HttpClient client = new();
        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            {"client_id", "54473e32-df8f-42e9-a649-9419b0dab9d3"},
            {"code", code},
            {"grant_type", "authorization_code"},
            {"redirect_uri", "https://mccteam.github.io/redirect.html"}
        });
        HttpResponseMessage response = client.PostAsync("https://login.live.com/oauth20_token.srf", content).Result;
        response.EnsureSuccessStatusCode();
        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
        return json["refresh_token"].ToString();
    }

    public static (string, string) GetMsAccessToken(string refreshToken)
    {
        HttpClient client = new();
        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            {"client_id", "54473e32-df8f-42e9-a649-9419b0dab9d3"},
            {"refresh_token", refreshToken},
            {"grant_type", "refresh_token"},
            {"redirect_uri", "https://mccteam.github.io/redirect.html"}
        });
        HttpResponseMessage response = client.PostAsync("https://login.live.com/oauth20_token.srf", content).Result;
        response.EnsureSuccessStatusCode();
        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
        return (json["access_token"].ToString(), json["refresh_token"].ToString());
    }

    public static (string, string) AuthenticateWithXBL(string msAccessToken)
    {
        HttpClient client = new();
        string content = "{\"Properties\":{\"AuthMethod\":\"RPS\",\"SiteName\":\"user.auth.xboxlive.com\",\"RpsTicket\":\"d=" + msAccessToken + "\"},\"RelyingParty\":\"http://auth.xboxlive.com\",\"TokenType\":\"JWT\"}";
        HttpResponseMessage response = client.PostAsync("https://user.auth.xboxlive.com/user/authenticate", new StringContent(content, Encoding.UTF8, "application/json")).Result;
        response.EnsureSuccessStatusCode();
        string responseContent = response.Content.ReadAsStringAsync().Result;
        JObject json = JObject.Parse(responseContent);
        return (json["Token"].ToString(), json["DisplayClaims"]["xui"][0]["uhs"].ToString());
    }

    public static string AuthenticateWithXSTS(string xblToken)
    {
        HttpClient client = new();
        string content = "{\"Properties\":{\"SandboxId\":\"RETAIL\",\"UserTokens\":[\"" + xblToken + "\"]},\"RelyingParty\":\"rp://api.minecraftservices.com/\",\"TokenType\":\"JWT\"}";
        HttpResponseMessage response = client.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", new StringContent(content, Encoding.UTF8, "application/json")).Result;
        response.EnsureSuccessStatusCode();
        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
        return json["Token"].ToString();
    }

    public static string GetMinecraftAccessToken(string xstsToken, string userHash)
    {
        HttpClient client = new();
        string content = "{\"identityToken\":\"XBL3.0 x=" + userHash + ";" + xstsToken + "\"}";
        HttpResponseMessage response = client.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", new StringContent(content, Encoding.UTF8, "application/json")).Result;
        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
        return json["access_token"].ToString();
    }

    public static MinecraftProfile? GetMcProfile(string minecraftAccessToken)
    {
        HttpClient client = new();
        HttpRequestMessage request = new(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.Add("Authorization", "Bearer " + minecraftAccessToken);
        HttpResponseMessage response = client.SendAsync(request).Result;
        response.EnsureSuccessStatusCode();
        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
        return new MinecraftProfile(json["name"].ToString(), json["id"].ToString(), minecraftAccessToken);
    }
    
    public static void RequireJoinServer(string hex, string accessToken, string uuid)
    {
        HttpClient client = new();
        string content = "{\"accessToken\":\"" + accessToken + "\",\"selectedProfile\":\"" + uuid + "\",\"serverId\":\"" + hex + "\"}";
        HttpResponseMessage response = client.PostAsync("https://sessionserver.mojang.com/session/minecraft/join", new StringContent(content, Encoding.UTF8, "application/json")).Result;
        response.EnsureSuccessStatusCode();
    }
}