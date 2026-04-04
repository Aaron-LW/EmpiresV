using Color = System.Drawing.Color;
using Smash.Graphics;
using System.Numerics;
using System.Net.Sockets;
using System.Net;
using Smash;

public class LobbyState : GameState
{
    private readonly MenuStateResult _menuStateResult;
    private string? _localIp;

    private Texture2D _hostCrown;

    public LobbyState(StateMachine stateMachine, Window window, MenuStateResult menuStateResult) : base(stateMachine, window)
    {
        _menuStateResult = menuStateResult;
        _localIp = GetLocalIp();

        _hostCrown = AssetManager.GetTexture("HostCrown");
    }

    public override void Update(double deltaTime)
    {
    }

    public override void Render(Renderer renderer)
    {
        renderer.RenderText(App.Font, "Lobby", new Vector2(20), Color.White);
        renderer.RenderText(App.Font, $"Ip: {_localIp}", new Vector2(App.Font.MeasureString("Lobby").X + 40, 20), Color.White);


        List<(string username, PlayerData data)> values;
        lock (_stateMachine.PlayerDataLock)
        {
            values = [(_menuStateResult.Username, new PlayerData() { Host = _menuStateResult.HostServer }), .. _stateMachine.PlayerData.Select(d => (d.Key, d.Value))];
        }

        Vector2 startPosition = new Vector2(40, 100);
        for (int i = 0; i < values.Count; i++)
        {
            Vector2 currentPosition = startPosition + new Vector2(0, 50 * i + 20 * i);
            Rectangle rectangle = new Rectangle(currentPosition, _window.Width - 80, 50);
            renderer.RenderFilledRectangle(rectangle, Color.FromArgb(40, 40, 40));
            if (_menuStateResult.Username == values[i].username) renderer.RenderRectangle(rectangle, Color.White);
            
            Vector2 textPosition = currentPosition + new Vector2(rectangle.Height / 2) - new Vector2(0, App.Font.MeasureString(values[i].username).Y / 2); 
            renderer.RenderText(App.Font, values[i].username, textPosition, Color.White);
            
            if (values[i].data.Host)
            {
                renderer.RenderTexture(_hostCrown, textPosition + new Vector2(App.Font.MeasureString(values[i].username).X + 10, 0), Color.White, 2);
            }
        }
    }

    public override StateResult Exit()
    {
        throw new NotImplementedException();
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
}