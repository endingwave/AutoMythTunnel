using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MineSharp.Protocol;

namespace AutoMythTunnel.Utils;

public static class NetworkUtils
{
    public static double Tcping(string host, int port)
    {
        using TcpClient client = new();
        Stopwatch stopwatch = new();
        stopwatch.Start();
        MinecraftClient.RequestServerStatus(host, (ushort)port);
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
    
    public static byte[] PostData(string url, byte[] data)
    {
        using WebClient client = new();
        byte[] response = client.UploadData(url, data);
        return response;
    }

    public static byte[] PostData(string url, Span<byte> data)
    {
        return PostData(url, data.ToArray());
    }
}