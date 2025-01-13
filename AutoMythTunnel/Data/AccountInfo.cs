namespace AutoMythTunnel.Data;

public class AccountInfo
{
    public string Uuid { get; init; }
    public string Username { get; init; }
    public string Password { get; init; }
    public string Attach { get; init; }
    public string Type { get; init; }
    public string User { get; init; }
    public bool IsHypixel21 { get; init; }
    public DateTime CreateDate { get; init; }
    public string Email { get; init; }
    public string Oa2ClientId { get; init; }
    public DateTime Oa2ExpireDate { get; init; }

    public bool Delete() => MythApi.DeleteAccount(Uuid);
}