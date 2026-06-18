using System.Net;
using System.Net.Sockets;

public class PlayerClient
{
    public required string Username { get; set; }
    public required bool Host { get; set; }
    public required byte Id { get; set; }

    public required TcpClient TcpClient { get; set; }
    public required PlayerData PlayerData { get; set; }

    public IPEndPoint? UdpEndPoint { get; set; }

    public SemaphoreSlim SendLock { get; } = new(1, 1);
}