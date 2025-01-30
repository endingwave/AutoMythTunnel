namespace AutoMythTunnel.Data;

public class MinecraftProfile(string name, string uuid, string accessToken)
{
    public string Name { get; } = name;
    public string Uuid { get; } = uuid;
    public string AccessToken { get; } = accessToken;
}