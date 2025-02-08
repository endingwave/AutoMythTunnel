using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoMythTunnel.Utils;

public class HttpUtils
{
    private static readonly Dictionary<string, string> DefaultHeaders = new()
    {
        { "Accept", "application/json, text/plain, */*" },
        { "Accept-Language", "zh-CN,zh;q=0.9" },
        { "Cache-Control", "no-cache" },
        { "Pragma", "no-cache" },
        { "Priority", "u=1, i" },
        { "Sec-Ch-Ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"" },
        { "Sec-Ch-Ua-Mobile", "?0" },
        { "Sec-Ch-Ua-Platform", "\"Windows\"" },
        { "Sec-Fetch-Dest", "empty" },
        { "Sec-Fetch-Mode", "cors" },
        { "Sec-Fetch-Site", "same-origin" },
        { "X-Kl-Kis-Ajax-Request", "Ajax_Request" },
        { "Referrer", "https://myth.galcraft.top/" }
    };
    
    public static string Get(string url, Dictionary<string, string>? headers = null)
    {
        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        foreach ((string key, string value) in DefaultHeaders)
        {
            request.Headers.Add(key, value);
        }
        if (headers != null)
        {
            foreach ((string key, string value) in headers)
            {
                request.Headers.Add(key, value);
            }
        }
        using HttpResponseMessage response = client.Send(request);
        return response.Content.ReadAsStringAsync().Result;
    }
    
    public static string Post(string url, string content, Dictionary<string, string>? headers = null)
    {
        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        foreach ((string key, string value) in DefaultHeaders)
        {
            request.Headers.Add(key, value);
        }
        if (headers != null)
        {
            foreach ((string key, string value) in headers)
            {
                request.Headers.Add(key, value);
            }
        }
        using HttpResponseMessage response = client.Send(request);
        return response.Content.ReadAsStringAsync().Result;
    }
    
    public static string Post(string url, JsonObject content, Dictionary<string, string>? headers = null)
    {
        return Post(url, content.ToString(), headers);
    }
    
    public static string Delete(string url, Dictionary<string, string>? headers = null)
    {
        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Delete, url);
        foreach ((string key, string value) in DefaultHeaders)
        {
            request.Headers.Add(key, value);
        }
        if (headers != null)
        {
            foreach ((string key, string value) in headers)
            {
                request.Headers.Add(key, value);
            }
        }
        using HttpResponseMessage response = client.Send(request);
        return response.Content.ReadAsStringAsync().Result;
    }
    
    public static JsonObject GetJson(string url, Dictionary<string, string>? headers = null)
    {
        string response;
        while (true)
        {
            response = Get(url, headers);
            try
            {
                return JsonNode.Parse(response).AsObject();
            }
            catch (Exception e)
            {
                continue;
            }
        }
    }
    
    public static JsonObject PostJson(string url, string content, Dictionary<string, string>? headers = null)
    {
        string response = Post(url, content, headers);
        return JsonNode.Parse(response).AsObject();
    }
    
    public static JsonObject PostJson(string url, JsonObject content, Dictionary<string, string>? headers = null)
    {
        return PostJson(url, content.ToString(), headers);
    }
    
    
    public static JsonObject DeleteJson(string url, Dictionary<string, string>? headers = null)
    {
        string response = Delete(url, headers);
        return JsonNode.Parse(response).AsObject();
    }
}