using System.Net;
using System.Net.Sockets;

public class Server
{
    private readonly List<PlayerClient> _clients = new();
    private readonly object _lock = new();

    private UdpClient? _udpServer;
    private bool _gameStarted = false;

    public async Task Main()
    {

        Console.Write("Starting TCP server...  ");

        TcpListener server = new TcpListener(IPAddress.IPv6Any, 5000);
        server.Server.DualMode = true;

        server.Start();

        Console.WriteLine("Success!");

        Console.Write("Starting UDP server...  ");

        _udpServer = new UdpClient(AddressFamily.InterNetworkV6);
        _udpServer.Client.DualMode = true;
        _udpServer.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, 5000));
        _ = HandleUdp();

        Console.WriteLine("Success!");
        Console.WriteLine("SERVER_READY");

        Console.WriteLine($"Server start timestamp: {DateTime.UtcNow}");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            NetworkStream clientStream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead = clientStream.Read(buffer);

            LoginPacket loginRequestPacket = new(buffer[0..bytesRead]);
            if (loginRequestPacket.Username == null) throw new Exception("Login request packet's username was null D:");

            Console.WriteLine($"Received Login request from {loginRequestPacket.Username}");

            bool host = _clients.Count == 0;

            if (_gameStarted)
            {
                Console.WriteLine($"Denying client because the game has already been started");

                await clientStream.WriteAsync(new ReadOnlyMemory<byte>([0x02]));
                client.Close();
                continue;
            }

            byte playerId = (byte)_clients.Count;

            if (_clients.Any(c => c.Username == loginRequestPacket.Username))
            {
                Console.WriteLine($"Denying client because username {loginRequestPacket.Username} has already been taken");

                await clientStream.WriteAsync(new ReadOnlyMemory<byte>([0x01]));
                client.Close();
                continue;
            }
            else
            {
                await clientStream.WriteAsync(new ReadOnlyMemory<byte>([0x00, host ? (byte)0x01 : (byte)0x00, playerId]));
            }


            PlayerClient playerClient = new PlayerClient()
            {
                TcpClient = client,
                Username = loginRequestPacket.Username,
                PlayerData = new() { Host = host, Username = loginRequestPacket.Username },
                Host = host,
                Id = playerId
            };

            lock (_lock)
            {
                _clients.Add(playerClient);
            }

            await Task.Run(async () => BroadCastPacketTcp(new JoinPacket(null) { Username = loginRequestPacket.Username }.Serialize(), playerClient));
            Console.WriteLine($"{loginRequestPacket.Username} has joined the server " + (host ? "as host" : ""));
            _ = HandleClientTcp(playerClient);
        }
    }

    private async Task HandlePacket(byte[] data, PlayerClient playerClient, UdpReceiveResult? udpReceiveResult = null)
    {
        byte id = data[0];

        switch (id)
        {
            case PacketId.PACKET_UPDATE_POS:
                PositionUpdatePacket positionUpdatePacket = new(data);
                
                if (Program.TCPOnly)
                    await BroadCastPacketTcp(data, playerClient);
                else
                    _ = BroadCastPacketUdp(data, playerClient);

                playerClient.PlayerData.X = positionUpdatePacket.X;
                playerClient.PlayerData.Y = positionUpdatePacket.Y;
                break;

            case PacketId.PACKET_PING:
                await BroadCastPacketTcp(data, playerClient);
                break;

            case PacketId.PACKET_START_GAME:
                await BroadCastPacketTcp(data, playerClient);
                _gameStarted = true;
                break;

            case PacketId.PACKET_UDP_INIT:
                if (udpReceiveResult != null)
                {
                    playerClient.UdpEndPoint = ((UdpReceiveResult)udpReceiveResult).RemoteEndPoint;
                    Console.WriteLine($"{playerClient.Username}'s Udp endpoint: {playerClient.UdpEndPoint}");
                    Console.WriteLine($"{playerClient.Username}'s Udp Address family: {playerClient.UdpEndPoint.AddressFamily}");
                }
                break;

            case PacketId.PACKET_REQUEST_DATA:
                foreach (PlayerClient otherClient in _clients)
                {
                    if (otherClient == playerClient) continue;
                    NetworkStream stream = playerClient.TcpClient.GetStream();
                    byte[] playerData = [..new PlayerDataPacket(null) { PlayerData = otherClient.PlayerData }.Serialize(), otherClient.Id];
                    byte[] framedData = [..BitConverter.GetBytes(playerData.Length), ..playerData];
                    await stream.WriteAsync(framedData);
                }
                break;
        }
    }

    private async Task HandleClientTcp(PlayerClient playerClient)
    {
        NetworkStream networkStream = playerClient.TcpClient.GetStream();

        byte[] readBuffer = new byte[1024];
        List<byte> receiveBuffer = new();

        try
        {
            while (true)
            {
                int bytesRead = await networkStream.ReadAsync(readBuffer);

                if (bytesRead == 0)
                {
                    break;            
                }

                receiveBuffer.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());

                while (TryExtractPacket(receiveBuffer, out byte[]? packet))
                {
                    await HandlePacket(packet!, playerClient);
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Receive loop crashed: " + ex);
        }
        finally
        {
            await BroadCastPacketTcp(new LeavePacket(null) { Username = playerClient.Username }.Serialize(), playerClient);
            Console.WriteLine($"{playerClient.Username} has left");

            lock (_lock)
            {
                _clients.Remove(playerClient);
            }

            playerClient.TcpClient.Dispose();

            if (_clients.Count == 0 && _gameStarted)
            {
                _gameStarted = false;
                Console.WriteLine("All clients have disconnected; Resetting game");
            }
        }
    }

    private async Task HandleUdp()
    {
        while (true)
        {
            UdpReceiveResult result = await _udpServer!.ReceiveAsync();
            byte playerId = result.Buffer[^1];

            PlayerClient? client = _clients.FirstOrDefault(c => c.Id == playerId);
            if (client != null)
            {
                await HandlePacket(result.Buffer[4..result.Buffer.Length], client, result);
            }
        }
    }

    private static bool TryExtractPacket(List<byte> buffer, out byte[]? packet)
    {
        packet = null;

        if (buffer.Count < 4)
            return false;

        int packetLength = BitConverter.ToInt32(buffer.GetRange(0, 4).ToArray());

        if (packetLength < 0)
            throw new InvalidDataException("Negative packet length");

        if (buffer.Count < 4 + packetLength)
            return false;

        packet = buffer.GetRange(4, packetLength).ToArray();

        buffer.RemoveRange(0, 4 + packetLength);

        return true;
    }

    private async Task BroadCastPacketTcp(byte[] data, PlayerClient sender)
    {
        foreach (PlayerClient playerClient in _clients)
        {
            if (sender.Username == playerClient.Username) continue;

            NetworkStream networkStream = playerClient.TcpClient.GetStream();

            await playerClient.SendLock.WaitAsync();

            try
            {
                await networkStream.WriteAsync(FramePacket(data, sender), 0, data.Length + 5);
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

    private async Task BroadCastPacketUdp(byte[] data, PlayerClient sender)
    {
        foreach (PlayerClient playerClient in _clients)
        {
            if (sender.Id == playerClient.Id) continue;

            await _udpServer!.SendAsync(FramePacket(data, sender), data.Length + 5, playerClient.UdpEndPoint);
        }
    }

    private byte[] FramePacket(byte[] packet, PlayerClient sender)
    {
        byte[] data = new byte[packet.Length + 5];
        BitConverter.GetBytes(packet.Length).CopyTo(data, 0);
        packet.CopyTo(data, 4);
        data[^1] = sender.Id;
        return data;
    }
}
