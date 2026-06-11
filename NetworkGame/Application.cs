using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SDL3;
using Smash;
using Smash.Graphics;
using Color = System.Drawing.Color;

public class App : Application 
{
    public const int POINT_SIZE = 25;
    public static Font Font = null!;

    public static float WindowWidth => _window!.Width;
    public static float WindowHeight => _window!.Height;

    private static Window? _window;
    private Renderer _renderer;

    private static NetworkStream? _networkStream;
    private static UdpClient? _udpClient;
    private static IPEndPoint? _serverEndPoint;

    private Process? _serverProcess;

    private float _elapsedTime;
    private double _fps;

    private IState? _currentState;

    public App(bool autoconnect, bool autohost)
    {
        CreateWindowAndRenderer("Networkgame", 1080, 800, out _window, out _renderer);
        _window.SetWindowResizable(true);

        AssetManager.SetDefaultScaleMode(ScaleMode.Nearest);
        AssetManager.SetAssetRootDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"));

        AssetManager.LoadFont("Roboto.ttf");
        Font = AssetManager.GetFont("Roboto", POINT_SIZE);

        AssetManager.LoadTexture("TextureAtlas.png", _renderer);
        AssetManager.AddTextureRegion("HostCrown", new TextureRegion("TextureAtlas", 0, 0, 19, 15));
        AssetManager.AddTextureRegion("Grass", new TextureRegion("TextureAtlas", 0, 16, 16, 16));
        AssetManager.AddTextureRegion("CoalTile", new TextureRegion("TextureAtlas", 16, 16, 16, 16));
        AssetManager.AddTextureRegion("mogus", new TextureRegion("TextureAtlas", 32, 0, 16, 16));
        AssetManager.AddTextureRegion("Selector", new TextureRegion("TextureAtlas", 0, 32, 16, 16));

        _renderer.SetVSyncEnabled(false);
        _renderer.SetRenderBlendMode(BlendMode.Blend);

        string dataFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmpiresV", "data.json");
        string? username = null;

        if (File.Exists(dataFile))
        {
            username = JsonSerializer.Deserialize<string>(File.ReadAllText(dataFile))!;
        }

        if (autoconnect)
        {
            SetState(new GamingState(new StateResult { PlayerData = new PlayerData() { Username = username ?? "Default name", Host = true, X = 0, Y = 0 }, Type = "lobby" }, Random.Shared.Next(100, 5000)));
        }
        else
        {
            SetState(new MenuState(null!));
        }

        string datafolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EmpiresV");
        if (!Directory.Exists(datafolderPath))
        {
            Directory.CreateDirectory(datafolderPath);
        }

        if (autohost)
        {
            HostServer(new MenuStateResult()
            {
                PlayerData = new PlayerData() { Username = username ?? "Default name", Host = true},
                Type = "menu",
                Ip = "127.0.0.1"
            });

            //SetState(new GamingState(new StateResult { PlayerData = new PlayerData() { Username = username ?? "Default name", Host = true, X = 0, Y = 0 }, Type = "lobby" }, Random.Shared.Next(100, 5000)));
        }
    }

    public override void Update(double deltaTime) 
    {
        _elapsedTime += (float)deltaTime;
        if (_elapsedTime > 0.1f)
        {
            _fps = 1f / deltaTime;
            _elapsedTime = 0f;
        }

        _currentState?.Update(deltaTime);
    }

    public override void Render() 
    {
        _currentState?.Render(_renderer);

        string fpsText = $"Fps: {(int)_fps}";
        _renderer.RenderText(Font, fpsText, new Vector2(WindowWidth - Font.MeasureString(fpsText).X - 20, 20), Color.White);

        _renderer.RenderPresent();
    }

    public override void End() 
    {
        SDL.StopTextInput(_window!.Handle);
        _window.Dispose();
        _renderer.Dispose();
        AssetManager.Dispose();
        _serverProcess?.Kill();
        _serverProcess?.WaitForExit();
        _serverProcess?.Dispose();
    }

    private void OnStateFinish(object? sender, EventArgs e)
    {
        if (e is StateFinishEventArgs stateFinishEventArgs)
        {
            if (stateFinishEventArgs.StateResult is MenuStateResult menuStateResult)
            {
                if (menuStateResult.PlayerData.Host)
                {
                    HostServer(menuStateResult);
                }
                else
                {
                    (bool connected, bool host) = TryConnect(menuStateResult.PlayerData.Username, menuStateResult.Ip);

                    if (host) Console.WriteLine("You are the host!");

                    if (connected)
                    {
                        Console.WriteLine("Connected to Server!");

                        menuStateResult.PlayerData.Host = host;
                        SetState(new LobbyState(menuStateResult, _serverEndPoint!.Address.ToString()));
                    }
                    else
                    {
                        SetState(new MenuState(new StateResult { PlayerData = null!, Type = "none"}));
                    }
                }
            }

            if (stateFinishEventArgs.StateResult is LobbyStateResult lobbyStateResult)
            {
                if (lobbyStateResult.Seed != 0)
                {
                    SetState(new GamingState(lobbyStateResult, lobbyStateResult.Seed));
                }
                else
                {
                    Random random = new();
                    int seed = random.Next(100, 5000);

                    SendPacketTcp(PacketId.PACKET_START_GAME, new StartGamePacket() { Username = stateFinishEventArgs.StateResult.PlayerData.Username, Seed = seed }).GetAwaiter().GetResult();
                    SetState(new GamingState(lobbyStateResult, seed));
                }
            }
        }
    }

    private void SetState(IState newState)
    {
        _currentState = newState;
        _currentState.StateFinish += OnStateFinish;
    }

    private void HostServer(MenuStateResult menuStateResult)
    {
        Console.WriteLine("Starting server...");

        _serverProcess = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetworkGame"),
                Arguments = "--server",
                RedirectStandardOutput = true,
                UseShellExecute = false
            },
        };
        _serverProcess.Start();
        menuStateResult.Ip = "127.0.0.1";

        _serverProcess.BeginOutputReadLine();
        _serverProcess.OutputDataReceived += async (sender, e) =>
        {
            bool startedServer = false;                        

            if (e.Data != null)
            {
                Console.WriteLine("[SERVER] " + e.Data);

                if (e.Data.Contains("SERVER_READY") && !startedServer)
                {
                    Console.WriteLine("Server has started!");
                    startedServer = true;


                    (bool success, bool host) = TryConnect(menuStateResult.PlayerData.Username, menuStateResult.Ip);
                    Console.WriteLine($"Server Ip: {GetLocalIp()}");

                    if (!success)
                    {
                        _serverProcess.Kill();
                        return;
                    }

                    Console.WriteLine($"Host: {host}");

                    menuStateResult.PlayerData.Host = host;
                    SetState(new LobbyState(menuStateResult, GetLocalIp()!));
                }
            }
        };
    }



    




























    // --- Fucking networking stuff ---

    public static async Task SendPacketTcp<T>(byte id, T packet) where T : Packet
    {
        if (_networkStream == null) return;

        ReadOnlyMemory<byte> buffer = new([id, .. Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet) + "\n")]);
        _ = Task.Run(async () => _networkStream.WriteAsync(buffer));
    }

    public static async Task SendPacketUdp<T>(byte id, T packet) where T : Packet
    {
        if (_udpClient == null) return;

        byte[] data = [id, .. Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet))];
        await _udpClient.SendAsync(data, data.Length, _serverEndPoint);
    }

    //(Could connect, is host)
    private (bool, bool) TryConnect(string username, string ip)
    {
        ip = ip.Trim();
        Console.WriteLine("Trying to connect as " + username);

        IPAddress[] addresses = Dns.GetHostAddresses(ip);

        TcpClient? tcpClient = null;
        int? connectedIndex = null;

        for (int i = 0; i < addresses.Length; i++)
        {
            try
            {
                Console.WriteLine($"Trying to connect to {addresses[i]}");

                tcpClient = new TcpClient(addresses[i].ToString(), 5000);
                connectedIndex = i;

                break;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Connection failed on {addresses[i]}: {ex.Message}");
            }
        }

        if (tcpClient == null || connectedIndex == null) throw new Exception("Couldn't connect to server: Server not found :(");
        NetworkStream networkStream = tcpClient.GetStream();


        byte[] login = [PacketId.PACKET_LOGIN, .. Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LoginPacket { Username = username }))];
        networkStream.Write(login);

        byte[] connectionBuffer = new byte[2];
        int bytesRead = networkStream.Read(connectionBuffer);

        if (connectionBuffer[0] != 0x00)
        {
            Console.Write("Couldn't connect to server: ");

            switch (connectionBuffer[0])
            {
                case 0x01:
                    Console.WriteLine("User with the same name already exists");
                    break;

                case 0x02:
                    Console.WriteLine("Game has already been started");
                    break;
            }

            networkStream.Close();
            tcpClient.Close();
            return (false, false);
        }
        else
        {
            bool host = connectionBuffer[1] == 0x01;

            _networkStream = networkStream;
            
            _udpClient = new UdpClient(AddressFamily.InterNetworkV6);
            _udpClient.Client.DualMode = true;

            _serverEndPoint = new IPEndPoint(addresses[(int)connectedIndex!], 5000);

            SendPacketUdp(PacketId.PACKET_UDP_INIT, new Packet { Username = username }).GetAwaiter();

            _ = Task.Run(async () => ReceivePackets(false));
            _ = Task.Run(async () => ReceivePackets(true));

            return (true, host);
        }
    }


    private string? GetLocalIp()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);

        var endPoint = socket.LocalEndPoint as IPEndPoint;
        if (endPoint != null)
        {
            return endPoint.Address.ToString();
        }
        
        return null;
    }

    private async Task ReceivePackets(bool udp)
    {
        byte[] buffer = new byte[1024];
        StringBuilder stringBuilder = new();

        while (true)
        {
            if (!udp)
            {
                int bytesRead = await _networkStream!.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Disconnected from server");
                    _networkStream = null;
                    _udpClient = null;
                    _serverEndPoint = null;
                    SetState(new MenuState(new StateResult() { PlayerData = null!, Type = "none" }));
                    break;
                }

                stringBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            else
            {
                UdpReceiveResult result = await _udpClient!.ReceiveAsync();
                stringBuilder.Append(Encoding.UTF8.GetString(result.Buffer));
            }

            while (stringBuilder.ToString().Contains("\n"))
            {
                string full = stringBuilder.ToString();
                int index = full.IndexOf("\n");

                string line = full.Substring(0, index);
                stringBuilder.Remove(0, index + 1);

                try
                {
                    _currentState?.ForwardPacket(Convert.ToByte(line[0]), line.Substring(1, line.Length - 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Json error: " + ex.Message);
                }
            }
        }
    }
}