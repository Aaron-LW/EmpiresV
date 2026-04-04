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

    public MenuState(StateMachine stateMachine, Window window) : base(stateMachine, window)
    {
        _usernameField = new InputField()
        {
            X = 150,
            Y = 20,
            Width = 400,
            Height = 50,
            BackgroundColor = Color.FromArgb(35, 35, 35),
            TextColor = Color.White,
        };

        _ipField = new InputField()
        {
            GetX = (InputField inputField) => _window.Width / 2 - inputField.Width / 2,
            GetY = (InputField inputField) => _window.Height / 2 - inputField.Height / 2,
            Width = 400,
            Height = 50,
            BackgroundColor = Color.FromArgb(35, 35, 35),
            TextColor = Color.White,
        };

        _inputFields.Add(_usernameField);
        _inputFields.Add(_ipField);

        _connectButton = new Button()
        {
            GetX = (Button button) => _window.Width / 2 - button.Width / 2,
            GetY = (Button button) => _window.Height / 2 + 40,
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
                    _stateMachine.FinishState();
                }
            }

            if (_hostButton.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
            {
                if (_usernameField.Text != string.Empty && _usernameField.Text != null)
                {
                    _hostServer = true;
                    _stateMachine.FinishState();
                }
            }
        }

        if (InputHandler.IsKeyPressed(SDL.Keycode.Return))
        {
            if (_usernameField.Text != string.Empty && _usernameField.Text != null &&  _ipField.Text != string.Empty && _ipField.Text != null)
            {
                _stateMachine.FinishState();
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
    }

    public override void Render(Renderer renderer)
    {
        renderer.RenderText(App.Font, "Username: ", new Vector2(20, 25), Color.White);
        _usernameField.Render(renderer);

        Vector2 ipFieldPosition = Vector2.Round(_ipField.Position + new Vector2(_ipField.Width / 2, -35) - new Vector2(App.Font.MeasureString("Server ip").X / 2, 0));
        renderer.RenderText(AssetManager.GetFont("Roboto", App.POINT_SIZE), "Server ip", ipFieldPosition, Color.White);
        _ipField.Render(renderer);

        _connectButton.Render(renderer);
        _hostButton.Render(renderer);
    }

    public override MenuStateResult Exit()
    {
        return new MenuStateResult() { Username = _usernameField.Text!, Ip = _ipField.Text!, HostServer = _hostServer };
    }
}