public class LobbyStateResult : StateResult
{
    public required int Seed { get; set; }
    public required List<(string username, string message)> ChatHistory { get; set; }
}