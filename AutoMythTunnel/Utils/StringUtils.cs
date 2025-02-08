using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spectre.Console;

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
    
    public static List<string> ExtractTextFields(string jsonString)
    {
        List<string> textValues = new();
        JsonNode? jsonNode = JsonNode.Parse(jsonString);

        if (jsonNode == null) return textValues;
        if (jsonNode["text"] != null)
        {
            textValues.Add(jsonNode["text"]!.ToString());
        }
        ExtractTextFieldsRecursive(jsonNode, textValues);

        return textValues;
    }

    private static void ExtractTextFieldsRecursive(JsonNode node, List<string> textValues)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode?> kvp in jsonObject)
            {
                if (kvp.Key == "text" && kvp.Value is JsonValue jsonValue)
                {
                    textValues.Add(jsonValue.ToString());
                }
                else
                {
                    ExtractTextFieldsRecursive(kvp.Value, textValues);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? item in jsonArray)
            {
                ExtractTextFieldsRecursive(item, textValues);
            }
        }
    }
    
    public static void PrintColoredMessage(string message)
    {
        // Example: Parse the message and apply colors
        string coloredMessage = message
            .Replace("§0", "[black]")
            .Replace("§1", "[blue]")
            .Replace("§2", "[green]")
            .Replace("§3", "[aqua]")
            .Replace("§4", "[red]")
            .Replace("§5", "[purple]")
            .Replace("§6", "[yellow]")
            .Replace("§7", "[white]")
            .Replace("§8", "[grey]")
            .Replace("§9", "[lightblue]")
            .Replace("§a", "[lightgreen]")
            .Replace("§b", "[lightaqua]")
            .Replace("§c", "[lightred]")
            .Replace("§d", "[lightpurple]")
            .Replace("§e", "[lightyellow]")
            .Replace("§f", "[white]")
            .Replace("§r", "[/]");

        AnsiConsole.MarkupLine(coloredMessage);
    }
}