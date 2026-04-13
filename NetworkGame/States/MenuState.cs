using System.Drawing;
using System.Numerics;
using SDL3;
using Smash.Graphics;
using Smash.Input;

public class MenuState : GameState
{
    private InputField _usernameField;
    private InputField _ipField;

    private Button _connectButton;
    private Button _hostButton;

    private bool _hostServer = false;

    private List<InputField> _inputFields = new();

    public MenuState(StateResult previousStateResult) : base(previousStateResult)
    {
        _usernameField = new InputField()
        {
            X = 150,
            Y = 20,
            Width = 400,
            Height = 50,
            BackgroundColor = Color.FromArgb(35, 35, 35),
            TextColor = Color.White,
            RequireInput = true
        };

        _ipField = new InputField()
        {
            GetX = (InputField inputField) => App.WindowWidth / 2 - inputField.Width / 2,
            GetY = (InputField inputField) => App.WindowHeight / 2 - inputField.Height / 2,
            Width = 400,
            Height = 50,
            BackgroundColor = Color.FromArgb(35, 35, 35),
            TextColor = Color.White,
        };

        _inputFields.Add(_usernameField);
        _inputFields.Add(_ipField);

        _connectButton = new Button()
        {
            GetX = (Button button) => App.WindowWidth / 2 - button.Width / 2,
            GetY = (Button button) => App.WindowHeight / 2 + 40,
            Width = 200,
            Height = 80,
            BackgroundColor = Color.FromArgb(40, 40, 40),
            TextColor = Color.White,
            Text = "Connect!"
        };

        _hostButton = new Button()
        {
            X = 20,
            Y = 90,
            Width = 200,
            Height = 80,
            BackgroundColor = Color.FromArgb(40, 40, 40),
            TextColor = Color.White,
            Text = "Host Server"
        };
    }

    public override void Update(double deltaTime)
    {
        if (InputHandler.IsLeftMousePressed())
        {
            foreach (InputField inputField in _inputFields)
            {
                if (inputField.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
                    inputField.Selected = true;
                else
                    inputField.Selected = false;
            }

            if (_connectButton.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
            {
                if (_usernameField.Text != string.Empty && _usernameField.Text != null &&  _ipField.Text != string.Empty && _ipField.Text != null)
                {
                    OnStateFinish(new MenuStateResult() { Ip = _ipField.Text, Type = "menu", PlayerData = new() { Username = _usernameField.Text, Host = _hostServer } });
                }
            }

            if (_hostButton.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
            {
                if (_usernameField.Text != string.Empty && _usernameField.Text != null)
                {
                    _hostServer = true;
                    OnStateFinish(new MenuStateResult() { Ip = "127.0.0.1", Type = "menu", PlayerData = new() { Username = _usernameField.Text, Host = _hostServer } });
                }
            }
        }

        if (InputHandler.IsKeyPressed(SDL.Keycode.Return))
        {
            if (_usernameField.Text != string.Empty && _usernameField.Text != null &&  _ipField.Text != string.Empty && _ipField.Text != null)
            {
                OnStateFinish(new MenuStateResult() { Ip = _ipField.Text, Type = "menu", PlayerData = new PlayerData() { Username = _usernameField.Text, Host = _hostServer } });
            }
        }

        if (InputHandler.TextInput != null)
        {
            foreach (InputField inputField in _inputFields)
            {
                if (inputField.Selected)
                    inputField.SendTextInput(InputHandler.TextInput);
            }
        }

        if (InputHandler.IsKeyPressed(SDL.Keycode.Backspace))
        {
            foreach (InputField inputField in _inputFields)
            {
                if (inputField.Selected)
                    inputField.SendTextInput("Backspace");
            }
        }

        bool setCursor = false;
        foreach (InputField hoverField in _inputFields)
        {
            if (hoverField.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
            {
                SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Text));
                setCursor = true;
            }
        }


        if (_connectButton.Rectangle.IsPositionInRectangle(InputHandler.MousePosition) ||
            _hostButton.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
        {
            SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Pointer));
            setCursor = true;
        }

        if (!setCursor)
        {
            SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Default));
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.FromArgb(20, 20, 20));

        renderer.RenderText(App.Font, "Username: ", new Vector2(20, 25), Color.White);
        _usernameField.Render(renderer);

        Vector2 ipFieldPosition = Vector2.Round(_ipField.Position + new Vector2(_ipField.Width / 2, -35) - new Vector2(App.Font.MeasureString("Server ip").X / 2, 0));
        renderer.RenderText(AssetManager.GetFont("Roboto", App.POINT_SIZE), "Server ip", ipFieldPosition, Color.White);
        _ipField.Render(renderer);

        _connectButton.Render(renderer);
        _hostButton.Render(renderer);
    }

    protected override void OnStateFinish<MenuStateResult>(MenuStateResult menuStateResult)
    {
        SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Default));
        base.OnStateFinish(menuStateResult);
    }
}