using System.Diagnostics;
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
    public const int POINT_SIZE = 25;
    public static Font Font = null!;

    public static float WindowWidth => _window!.Width;
    public static float WindowHeight => _window!.Height;

    private static Window? _window;
    private Renderer _renderer;

    private NetworkStream? _networkStream;
    private UdpClient? _udpClient;
    private IPEndPoint? _serverEndPoint;

    private Process? _serverProcess;

    private float _elapsedTime;
    private float _fps;

    private IState? _currentState;

    public App() 
    {
        CreateWindowAndRenderer("Networkgame", 1080, 800, out _window, out _renderer);
        _window.SetWindowResizable(true);
        SDL.StartTextInput(_window.Handle);

        NotificationManager.Window = _window;

        AssetManager.SetDefaultScaleMode(ScaleMode.Nearest);
        AssetManager.SetAssetRootDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"));

        AssetManager.LoadFont("Roboto.ttf");
        Font = AssetManager.GetFont("Roboto", POINT_SIZE);

        AssetManager.LoadTexture("TextureAtlas.png", _renderer);
        AssetManager.AddTextureRegion("HostCrown", new TextureRegion("TextureAtlas", 0, 0, 19, 15));

        _renderer.SetVSyncEnabled(false);
    }

    public override void Start()
    {
        SetState(new MenuState());
    }

    public override void Update(double deltaTime) 
    {
        _elapsedTime += (float)deltaTime;
        if (_elapsedTime > 0.3f)
        {
            _elapsedTime = 0;
            _fps = 1 / (float)deltaTime;
        }

        _currentState?.Update(deltaTime);
        NotificationManager.Update(deltaTime);

        if (InputHandler.IsLeftMousePressed())
        {
            NotificationManager.Notify("test test test", NotificationLevel.Normal);
        }
    }

    public override void Render() 
    {
        _renderer.Clear(Color.FromArgb(20, 20, 20));

        _renderer.RenderText(Font, $"Fps: {_fps}", new Vector2(WindowWidth - Font.MeasureString($"Fps: {_fps}").X - 20, 20), Color.White);

        _currentState?.Render(_renderer);
        NotificationManager.Render(_renderer);

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
                if (menuStateResult.HostServer)
                {
                    Console.WriteLine("Starting server...");

                    _serverProcess = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetworkGame"),
                            Arguments = "server start lol",
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

                                string? localIp = GetLocalIp();
                                if (localIp != null)
                                {
                                    Console.WriteLine($"Server Ip: {localIp}");
                                }

                                TryConnect(menuStateResult.Username, menuStateResult.Ip, true);
                                SetState(new LobbyState(menuStateResult.Username, menuStateResult.HostServer));
                            }
                        }
                    };

                }
                else
                {
                    Console.WriteLine($"Username: {menuStateResult.Username}");
                    Console.WriteLine($"Ip: {menuStateResult.Ip}");

                    string? localIp = GetLocalIp();
                    if (localIp != null)
                    {
                        Console.WriteLine($"Server Ip: {localIp}");
                    }

                    bool connected = TryConnect(menuStateResult.Username, menuStateResult.Ip);

                    if (connected)
                    {
                        Console.WriteLine("Connected to Server!");
                        SetState(new LobbyState(menuStateResult.Username, menuStateResult.HostServer));
                    }
                    else
                    {
                        Console.WriteLine("Couldn't connect to Server");
                        SetState(new MenuState());
                    }
                }
            }
        }
    }

    private void SetState(IState newState)
    {
        _currentState = newState;
        _currentState.StateFinish += OnStateFinish;
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

    private bool TryConnect(string username, string ip, bool host = false)
    {
        Console.WriteLine("Trying to connect as " + username);

        TcpClient tcpClient = new TcpClient(ip, 5000);
        NetworkStream networkStream = tcpClient.GetStream();

        networkStream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LoginPacket { Type = "login", Username = username, Host = host })));

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
            SendPacketUdp(new Packet { Type = "udp_init", Username = username }).GetAwaiter();

            //_stateMachine.SetConnectionData(_networkStream, _udpClient, _serverEndPoint);

            _ = Task.Run(async () => ReceivePackets(false));
            _ = Task.Run(async () => ReceivePackets(true));

            return true;
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
                    SetState(new MenuState());
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

                string line = full.Substring(0, index).Trim();
                stringBuilder.Remove(0, index + 1);

                try
                {
                    Packet? packet = JsonSerializer.Deserialize<Packet>(line);

                    if (packet != null)
                        _currentState?.ForwardPacket(packet, line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Json error: " + ex.Message);
                }
            }
        }
    }
}