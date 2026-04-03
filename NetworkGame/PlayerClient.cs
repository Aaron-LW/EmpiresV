using System.Net;
using System.Net.Sockets;

public class PlayerClient
{
    public required TcpClient TcpClient { get; set; }
    public required string Username { get; set; }
    public required PlayerData PlayerData { get; set; }
    public IPEndPoint? UdpEndPoint { get; set; }
}