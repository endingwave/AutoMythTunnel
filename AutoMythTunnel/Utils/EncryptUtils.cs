using System.Text;

namespace AutoMythTunnel.Utils;

public class EncryptUtils
{
    public static string Encode(string raw, string iv)
    {
        byte[] rawBytes = Encoding.UTF8.GetBytes(raw);
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv);
        byte[] encodedBytes = new byte[rawBytes.Length];
        for (int i = 0; i < rawBytes.Length; i++)
        {
            encodedBytes[i] = (byte)(rawBytes[i] ^ ivBytes[i % ivBytes.Length]);
        }
        return Encoding.UTF8.GetString(encodedBytes);
    }
    
    public static string Decode(string encoded, string iv)
    {
        byte[] encodedBytes = Encoding.UTF8.GetBytes(encoded);
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv);
        byte[] rawBytes = new byte[encodedBytes.Length];
        for (int i = 0; i < encodedBytes.Length; i++)
        {
            rawBytes[i] = (byte)(encodedBytes[i] ^ ivBytes[i % ivBytes.Length]);
        }
        return Encoding.UTF8.GetString(rawBytes);
    }
    
    public static byte[] EncodeBytes(byte[] raw, string iv)
    {
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv);
        byte[] encodedBytes = new byte[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            encodedBytes[i] = (byte)(raw[i] ^ ivBytes[i % ivBytes.Length]);
        }
        return encodedBytes;
    }
    
    public static byte[] DecodeBytes(byte[] encoded, string iv)
    {
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv);
        byte[] rawBytes = new byte[encoded.Length];
        for (int i = 0; i < encoded.Length; i++)
        {
            rawBytes[i] = (byte)(encoded[i] ^ ivBytes[i % ivBytes.Length]);
        }
        return rawBytes;
    }
}