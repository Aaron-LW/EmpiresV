using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class Server
{
    private readonly List<PlayerClient> _clients = new();
    private readonly object _lock = new();

    private UdpClient? _udpServer;
    private bool _gameStarted = false;

    public async Task Main()
    {
        Console.Write("Starting TCP server...  ");

        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();

        Console.WriteLine("Success!");

        Console.Write("Starting UDP server...  ");

        _udpServer = new UdpClient(5000);
        _ = HandleUdp();

        Console.WriteLine("Success!");
        Console.WriteLine("SERVER_READY");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            NetworkStream clientStream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead = clientStream.Read(buffer);

            LoginPacket? loginRequestPacket = JsonSerializer.Deserialize<LoginPacket>(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            if (loginRequestPacket == null) throw new Exception("Login request packet was null D:");

            Console.WriteLine($"Got login request packet from {loginRequestPacket.Username}");

            bool host = _clients.Count == 0;

            if (_gameStarted)
            {
                Console.WriteLine($"Denying client because the game has already been started");

                await clientStream.WriteAsync(new ReadOnlyMemory<byte>([00000002]));
                client.Close();
                continue;
            }

            if (_clients.Any(c => c.Username == loginRequestPacket.Username))
            {
                Console.WriteLine($"Denying client because username {loginRequestPacket.Username} has already been taken");

                await clientStream.WriteAsync(new ReadOnlyMemory<byte>([00000001]));
                client.Close();
                continue;
            }
            else
            {
                await clientStream.WriteAsync(new ReadOnlyMemory<byte>([00000000, host ? (byte)00000001 : (byte)00000000]));
            }


            PlayerClient playerClient = new PlayerClient()
            {
                TcpClient = client,
                Username = loginRequestPacket.Username,
                PlayerData = new() { Host = host, Username = loginRequestPacket.Username },
                Host = host
            };

            lock (_lock)
            {
                _clients.Add(playerClient);
            }

            await Task.Run(async () => BroadCastPacketTcp(new JoinPacket { Type = "join", Username = loginRequestPacket.Username, NewJoin = true}));
            Console.WriteLine($"{loginRequestPacket.Username} has joined the server " + (host ? "as host" : ""));
            _ = HandleClientTcp(playerClient);
        }
    }

    private async Task HandleClientTcp(PlayerClient playerClient)
    {
        NetworkStream networkStream = playerClient.TcpClient.GetStream();

        foreach (PlayerClient alreadyJoinedClient in _clients.ToList())
        {
            if (alreadyJoinedClient.Username == playerClient.Username) continue;
            Console.WriteLine($"Sending player data packet from {alreadyJoinedClient.Username} to {playerClient.Username}");
            await networkStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new PlayerDataPacket { Type = "playerData", Username = alreadyJoinedClient.Username, PlayerData = alreadyJoinedClient.PlayerData }) + "\n"));
        }

        StringBuilder stringBuilder = new();
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    await BroadCastPacketTcp(new LeavePacket { Type = "leave", Username = playerClient.Username});
                    Console.WriteLine($"{playerClient.Username} has left");

                    lock (_lock)
                    {
                        _clients.Remove(playerClient);
                    }

                    if (_clients.Count == 0)
                    {
                        _gameStarted = false;
                        Console.WriteLine("All clients have disconnected; Resetting game");
                    }

                    break;
                }

                stringBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                while (stringBuilder.ToString().Contains("\n"))
                {
                    string full = stringBuilder.ToString();
                    int index = full.IndexOf("\n");

                    string line = full.Substring(0, index).Trim();
                    stringBuilder.Remove(0, index + 1);

                    await HandlePacket(line, playerClient);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Receive loop crashed: " + ex);
        }
    }

    private async Task HandlePacket(string json, PlayerClient playerClient, UdpReceiveResult? udpReceiveResult = null)
    {
        Packet? packet = JsonSerializer.Deserialize<Packet>(json);

        if (packet != null)
            {
                switch (packet.Type)
                {
                    case "update_pos":
                        PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                        
                        if (Program.TCPOnly)
                            await BroadCastPacketTcp(positionUpdatePacket);
                        else
                            _ = BroadCastPacketUdp(positionUpdatePacket);

                        playerClient.PlayerData.X = positionUpdatePacket.X;
                        playerClient.PlayerData.Y = positionUpdatePacket.Y;
                        break;

                    case "ping":
                        PingPacket pingPacket = JsonSerializer.Deserialize<PingPacket>(json)!;
                        await BroadCastPacketTcp(pingPacket);
                        break;

                    case "start_game":
                        StartGamePacket startGamePacket = JsonSerializer.Deserialize<StartGamePacket>(json)!;
                        await BroadCastPacketTcp(startGamePacket);
                        _gameStarted = true;
                        break;

                    case "place_tile":
                        PlaceTilePacket placeTilePacket = JsonSerializer.Deserialize<PlaceTilePacket>(json)!;
                        await BroadCastPacketTcp(placeTilePacket);
                        break;

                    case "udp_init":
                        if (udpReceiveResult != null)
                        {
                            playerClient.UdpEndPoint = ((UdpReceiveResult)udpReceiveResult).RemoteEndPoint;
                            Console.WriteLine($"{playerClient.Username}'s Udp endpoint: {playerClient.UdpEndPoint}");
                        }

                        break;
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

            PlayerClient? client = _clients.FirstOrDefault(c => c.Username == packet!.Username);
            if (client != null)
            {
                await HandlePacket(json, client, result);
            }
        }
    }

    private async Task BroadCastPacketTcp<T>(T packet) where T : Packet
    {
        string json = JsonSerializer.Serialize(packet) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);

        foreach (PlayerClient playerClient in _clients)
        {
            if (packet.Username == playerClient.Username) continue;

            NetworkStream networkStream = playerClient.TcpClient.GetStream();

            await playerClient.SendLock.WaitAsync();

            try
            {
                await networkStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send to {playerClient.Username} failed: {ex.Message}");
            }
            finally
            {
                playerClient.SendLock.Release();
            }
        }
    }

    private async Task BroadCastPacketUdp<T>(T packet) where T : Packet
    {
        foreach (PlayerClient playerClient in _clients)
        {
            if (packet.Username == playerClient.Username) continue;

            byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet) + "\n");
            await _udpServer!.SendAsync(data, data.Length, playerClient.UdpEndPoint);
        }
    }
}