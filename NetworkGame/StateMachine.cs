using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Smash.Graphics;

public class StateMachine
{
    public IState? CurrentState { get; private set; }
    public StateResult? LastStateResult;
    public bool StateFinished = true;

    public Dictionary<string, PlayerData> PlayerData { get; private set; } = new();
    public readonly object PlayerDataLock = new();

    private NetworkStream? _networkStream;
    private UdpClient? _udpClient;
    private IPEndPoint? _serverEndPoint;

    public void SetState(IState newState)
    {
        if (StateFinished == false) return;
        CurrentState = newState;
        LastStateResult = null;
        StateFinished = false;
    }

    public void Update(double deltaTime)
    {
        CurrentState?.Update(deltaTime);
    }

    public void Render(Renderer renderer)
    {
        CurrentState?.Render(renderer);
    }

    public void FinishState()
    {
        StateResult? stateResult = CurrentState?.Exit();
        if (stateResult != null)
        {
            StateFinished = true;
            LastStateResult = stateResult;
            CurrentState = null;
        }
    }

    public void ForwardPacket(Packet packet, string json)
    {
        switch (packet.Type)
        {
            case "posUpdate":
                PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                PlayerData[positionUpdatePacket.Username].X = positionUpdatePacket.X;
                PlayerData[positionUpdatePacket.Username].Y = positionUpdatePacket.Y;
                break;

            case "join":
                JoinPacket joinPacket = JsonSerializer.Deserialize<JoinPacket>(json)!;
                Console.WriteLine("Received Join packet from " + joinPacket.Username);
                PlayerData.Add(joinPacket.Username, new PlayerData());
                break;

            case "leave":
                LeavePacket leavePacket = JsonSerializer.Deserialize<LeavePacket>(json)!;
                Console.WriteLine("Received leave packet from " + leavePacket.Username);
                PlayerData.Remove(leavePacket.Username);
                break;

            case "playerData":
                PlayerDataPacket playerDataPacket = JsonSerializer.Deserialize<PlayerDataPacket>(json)!;
                Console.WriteLine("Received player data packet from " + playerDataPacket.Username);
                if (playerDataPacket.PlayerData.Host) Console.WriteLine($"{playerDataPacket.Username} is the host");
                lock (PlayerDataLock)
                {
                    PlayerData[playerDataPacket.Username] = playerDataPacket.PlayerData;
                }
                break;

            default:
                Console.WriteLine("Received unknown packet type");
                break;
        }
    }

    public async Task SendPacketTcp<T>(T packet) where T : Packet
    {
        if (_networkStream == null) return;
        _ =  Task.Run(async () => _networkStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet) + "\n")));
    }

    public async Task SendPacketUdp<T>(T packet) where T : Packet
    {
        if (_udpClient == null) return;
        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet) + "\n");
        await _udpClient.SendAsync(data, data.Length, _serverEndPoint);
    }

    public void SetConnectionData(NetworkStream networkStream, UdpClient udpClient, IPEndPoint serverEndpoint)
    {
        _networkStream = networkStream;
        _udpClient = udpClient;
        _serverEndPoint = serverEndpoint;
    }
}