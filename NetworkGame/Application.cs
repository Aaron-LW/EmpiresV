using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SDL3;
using Smash;
using Smash.Graphics;
using Smash.Input;
using Color = System.Drawing.Color;

public class App : Application 
{
    private Dictionary<string, PlayerData> _playerData = new();

    private Window _window;
    private Renderer _renderer;

    private NetworkStream? _networkStream;
    private UdpClient? _udpClient;
    private IPEndPoint? _serverEndPoint;

    private float _elapsedTime;
    private float _fps;

    public App() 
    {
        CreateWindowAndRenderer("Networkgame", 1080, 800, out _window, out _renderer);
        _window.SetWindowResizable(true);
        SDL.StartTextInput(_window.Handle);

        AssetManager.SetDefaultScaleMode(ScaleMode.Nearest);
        AssetManager.SetAssetRootDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"));

        AssetManager.LoadFont("Roboto.ttf");

        _renderer.SetVSyncEnabled(false);
    }

    public override void Update(double deltaTime) 
    {
        _elapsedTime += (float)deltaTime;
        if (_elapsedTime > 0.3f)
        {
            _elapsedTime = 0;
            _fps = 1 / (float)deltaTime;
        }

        if (InputHandler.IsKeyPressed(SDL.Keycode.C))
        {
            bool connected = TryConnect(Random.Shared.Next(0, 10000).ToString(), "127.0.0.1");
            if (connected) Console.WriteLine("Connected!");
            else Console.WriteLine("Couldn't connect");
        }
    }

    public override void Render() 
    {
        _renderer.Clear(Color.CornflowerBlue);

        _renderer.RenderText(AssetManager.GetFont("Roboto", 20), $"Fps: {_fps}", new Vector2(20), Color.White);

        _renderer.RenderPresent();
    }

    public override void End() 
    {
        SDL.StopTextInput(_window.Handle);
        _window.Dispose();
        _renderer.Dispose();
        AssetManager.Dispose();
    }

    public void SendTextInput(string? input)
    {
        if (input == null) return;
    }

    

























    // --- Fucking networking stuff ewww ---

    private async Task SendPacketTcp<T>(T packet) where T : Packet
    {
        if (_networkStream == null) return;
        _ =  Task.Run(async () => _networkStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet))));
    }

    private async Task SendPacketUdp<T>(T packet) where T : Packet
    {
        if (_udpClient == null) return;
        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet));
        await _udpClient.SendAsync(data, data.Length, _serverEndPoint);
    }

    private bool TryConnect(string username, string ip)
    {
        Console.WriteLine("Trying to connect as " + username);

        TcpClient tcpClient = new TcpClient(ip, 5000);
        NetworkStream networkStream = tcpClient.GetStream();

        networkStream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LoginPacket { Type = "login", Username = username })));

        byte[] connectionBuffer = new byte[1024];
        int bytesRead = networkStream.Read(connectionBuffer);

        string connectionResponse = Encoding.UTF8.GetString(connectionBuffer, 0, bytesRead);

        if (connectionResponse == "Forbidden")
        {
            networkStream.Close();
            tcpClient.Close();
            return false;
        }
        else
        {
            _networkStream = networkStream;
            
            _udpClient = new UdpClient(0);
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), 5001);
            _ = SendPacketUdp(new Packet { Type = "udp_init", Username = username });

            _ = Task.Run(async () => ReceivePackets(false));
            _ = Task.Run(async () => ReceivePackets(true));

            return true;
        }
    }

    private async Task ReceivePackets(bool udp)
    {
        byte[] buffer = new byte[1024];

        while (true)
        {
            string json = "";

            if (!udp)
            {
                int bytesRead = await _networkStream!.ReadAsync(buffer, 0, buffer.Length);
                json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }
            else
            {
                UdpReceiveResult result = await _udpClient!.ReceiveAsync();
                json = Encoding.UTF8.GetString(result.Buffer);
            }

            Packet? packet = JsonSerializer.Deserialize<Packet>(json);

            if (packet != null)
            {
                switch (packet.Type)
                {
                    case "posUpdate":
                        PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                        _playerData[positionUpdatePacket.Username].X = positionUpdatePacket.X;
                        _playerData[positionUpdatePacket.Username].Y = positionUpdatePacket.Y;
                        break;

                    case "join":
                        JoinPacket joinPacket = JsonSerializer.Deserialize<JoinPacket>(json)!;
                        Console.WriteLine("Received Join packet from " + joinPacket.Username);
                        _playerData.Add(joinPacket.Username, new PlayerData());
                        break;

                    case "leave":
                        LeavePacket leavePacket = JsonSerializer.Deserialize<LeavePacket>(json)!;
                        Console.WriteLine("Received leave packet from " + leavePacket.Username);
                        _playerData.Remove(leavePacket.Username);
                        break;

                    case "playerData":
                        PlayerDataPacket playerDataPacket = JsonSerializer.Deserialize<PlayerDataPacket>(json)!;
                        Console.WriteLine("Received player data packet from " + playerDataPacket.Username);
                        _playerData.Add(playerDataPacket.Username, playerDataPacket.PlayerData);
                        break;

                    default:
                        Console.WriteLine("Received unknown packet type");
                        break;
                }
            }
        }
    }
}