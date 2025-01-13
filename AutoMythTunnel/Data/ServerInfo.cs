namespace AutoMythTunnel.Data;

public class ServerInfo
{
    public string Id { get; set; }
    public string IP { get; set; }
    public string City { get; set; }
    public string Status { get; set; }
    public bool IsLost { get; set; }
    public string GameServerAddress { get; set; }
    public DateTime ExpireDate { get; set; }
    
    public bool Delete() => MythApi.DeleteServer(Id);
    public bool Renew() => MythApi.RenewServer(Id);
}