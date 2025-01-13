namespace AutoMythTunnel.Data;

[Serializable]
public record UserInfo
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Role { get; set; }
    public required string TokenName { get; set; }
    public required string TokenValue { get; set; }
    
    public UserInfo ReLogin() => MythApi.Login(Username, Password);
}