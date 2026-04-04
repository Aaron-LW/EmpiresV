using Color = System.Drawing.Color;
using System.Numerics;
using Smash.Graphics;
using Smash;

public class InputField
{
    private float _x;
    private float _y;

    public float X { get => GetX != null ? GetX.Invoke(this) : _x; set => _x = value; }
    public float Y { get => GetY != null ? GetY.Invoke(this) : _y; set => _y = value; }
    public Vector2 Position => new Vector2(X, Y);

    public Func<InputField, float>? GetX;
    public Func<InputField, float>? GetY;

    public float Width;
    public float Height;
    public Vector2 Bounds => new Vector2(Width, Height);

    public Rectangle Rectangle => new Rectangle(X, Y, Width, Height);

    public Color BackgroundColor;

    public string? Text;
    public float TextX => X + Width / 2 - App.Font.MeasureString(Text!).X / 2;
    public float TextY => Y + Height / 2 - App.Font.MeasureString(Text!).Y / 2;
    public Vector2 TextPosition => new Vector2(TextX, TextY);

    public Color TextColor;

    public bool Selected = false;

    public void Render(Renderer renderer)
    {
        renderer.RenderFilledRectangle(Rectangle, BackgroundColor);

        if (Text == null || Text == string.Empty) renderer.RenderRectangle(Rectangle, Color.Red);
        if (Text != null) renderer.RenderText(App.Font, Text, Vector2.Round(TextPosition), TextColor);
        if (Selected) renderer.RenderRectangle(Rectangle, Color.White);
    }

    public void SendTextInput(string input)
    {
        if (input == "Backspace" && Text != null)
        {
            if (Text.Length > 0)
            {
                Text = Text.Remove(Text.Length - 1, 1);
            }
            return;
        }

        Text += input;
    }
}