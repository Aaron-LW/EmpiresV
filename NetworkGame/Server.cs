using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class Server
{
    private readonly List<PlayerClient> _clients = new();
    private readonly object _lock = new();

    private UdpClient? _udpServer;

    public async Task Main()
    {
        Console.Write("Starting TCP server...  ");

        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();

        Console.WriteLine("Success!");

        Console.Write("Starting UDP server...  ");

        _udpServer = new UdpClient(5001);
        _ = HandleUdp();

        Console.WriteLine("Success!");
        Console.WriteLine();

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            NetworkStream clientStream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead = clientStream.Read(buffer);

            LoginPacket? loginRequestPacket = JsonSerializer.Deserialize<LoginPacket>(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            if (loginRequestPacket == null) throw new Exception("Login request packet was null D:");

            if (_clients.Any(c => c.Username == loginRequestPacket.Username))
            {
                Console.WriteLine($"Denying client because username {loginRequestPacket.Username} has already been taken");

                await clientStream.WriteAsync(Encoding.UTF8.GetBytes("Forbidden"));
                client.Close();
                continue;
            }
            else
            {
                await clientStream.WriteAsync(Encoding.UTF8.GetBytes("Ok"));
            }

            PlayerClient playerClient = new PlayerClient()
            {
                TcpClient = client,
                Username = loginRequestPacket.Username,
                PlayerData = new(),
            };

            lock (_lock)
            {
                _clients.Add(playerClient);
            }

            _ = Task.Run(async () => BroadCastPacketTcp(new JoinPacket { Type = "join", Username = loginRequestPacket.Username, NewJoin = true}));
            Console.WriteLine($"{loginRequestPacket.Username} has joined the server");
            _ = Task.Run(async () => HandleClientTcp(playerClient));
        }
    }

    private async Task HandleClientTcp(PlayerClient playerClient)
    {
        NetworkStream networkStream = playerClient.TcpClient.GetStream();

        foreach (PlayerClient alreadyJoinedClient in _clients)
        {
            if (alreadyJoinedClient == playerClient) continue;
            await networkStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new PlayerDataPacket { Type = "playerData", Username = alreadyJoinedClient.Username, PlayerData = alreadyJoinedClient.PlayerData })));
        }

        byte[] buffer = new byte[1024];
        while (true)
        {
            int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                _ = Task.Run(async () => BroadCastPacketTcp(new LeavePacket { Type = "leave", Username = playerClient.Username}));
                Console.WriteLine($"{playerClient.Username} has left");

                lock (_lock)
                {
                    _clients.Remove(playerClient);
                }
                break;
            }

            string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Packet? packet = JsonSerializer.Deserialize<Packet>(json);

            if (packet != null)
            {
                switch (packet.Type)
                {
                    case "posUpdate":
                        PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                        _ = BroadCastPacketTcp(positionUpdatePacket);
                        _clients[_clients.IndexOf(playerClient)].PlayerData.X = positionUpdatePacket.X;
                        _clients[_clients.IndexOf(playerClient)].PlayerData.Y = positionUpdatePacket.Y;
                        break;
                }
            }
        }
    }

    private async Task HandleUdp()
    {
        while (true)
        {
            UdpReceiveResult result = await _udpServer!.ReceiveAsync();

            string json = Encoding.UTF8.GetString(result.Buffer);
            Packet? packet = JsonSerializer.Deserialize<Packet>(json);

            if (packet != null)
            {
                PlayerClient? client = _clients.FirstOrDefault(c => c.Username == packet.Username);
                switch (packet.Type)
                {
                    case "udp_init":
                        if (client != null)
                        {
                            client.UdpEndPoint = result.RemoteEndPoint;
                            Console.WriteLine($"{client.Username}'s Udp endpoint: {client.UdpEndPoint}");
                        }

                        break;

                    case "posUpdate":
                        PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;  

                        if (client != null)
                        {
                            client.PlayerData.X = positionUpdatePacket.X;
                            client.PlayerData.Y = positionUpdatePacket.Y;
                        }

                        await BroadCastPacketUdp(positionUpdatePacket);
                        break;
                }
            }
        }
    }

    private async Task BroadCastPacketTcp<T>(T packet) where T : Packet
    {
        foreach (PlayerClient playerClient in _clients)
        {
            if (packet.Username == playerClient.Username) continue;

            NetworkStream networkStream = playerClient.TcpClient.GetStream();
            _ = Task.Run(async () => networkStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet))));
        }
    }

    private async Task BroadCastPacketUdp<T>(T packet) where T : Packet
    {
        foreach (PlayerClient playerClient in _clients)
        {
            if (packet.Username == playerClient.Username) continue;

            byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet));
            _ = _udpServer!.SendAsync(data, data.Length, playerClient.UdpEndPoint);
        }
    }
}