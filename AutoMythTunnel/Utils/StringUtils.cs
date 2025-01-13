using System.Security.Cryptography;
using System.Text;

namespace AutoMythTunnel.Utils;

public static class StringUtils
{
    public static string GetMd5(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = MD5.HashData(inputBytes);
        StringBuilder sb = new();
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
    
    public static string HideSensitiveInfo(string input)
    {
        if (input.Length < 4)
        {
            return string.Concat(Enumerable.Repeat("*", input.Length));
        } 
        else
        {
            return string.Concat(Enumerable.Repeat("*", input.Length - 2)) + input[^2..];
        }
    }
}