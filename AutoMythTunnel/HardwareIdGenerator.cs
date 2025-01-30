using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace AutoMythTunnel;

public class HardwareIdGenerator
{
    public static string Generate()
    {
        string cpuId = GetCpuId();
        string motherboardSerial = GetMotherboardSerial();
        string diskDriveSerial = GetDiskDriveSerial();

        string combinedInfo = $"{cpuId}-{motherboardSerial}-{diskDriveSerial}";
        return ComputeSha256Hash(combinedInfo);
    }

    private static string GetCpuId()
    {
        using ManagementObjectSearcher searcher = new("select ProcessorId from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? obj = (ManagementObject)o;
            return obj["ProcessorId"]?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string GetMotherboardSerial()
    {
        using ManagementObjectSearcher searcher = new("select SerialNumber from Win32_BaseBoard");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? obj = (ManagementObject)o;
            return obj["SerialNumber"]?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string GetDiskDriveSerial()
    {
        using ManagementObjectSearcher searcher = new("select SerialNumber from Win32_DiskDrive");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? obj = (ManagementObject)o;
            return obj["SerialNumber"]?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }
    
    public static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}