public class StateResult
{
    public required PlayerData PlayerData;
    public Dictionary<byte, PlayerData>? PeerData; 

    public required string Type;
}