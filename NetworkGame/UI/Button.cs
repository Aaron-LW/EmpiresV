using System.Numerics;
using Smash;
using Smash.Graphics;
using Smash.Input;
using Color = System.Drawing.Color;

public class Button
{
    private float _x;
    private float _y;

    public float X { get => GetX != null ? GetX.Invoke(this) : _x; set => _x = value; }
    public float Y { get => GetY != null ? GetY.Invoke(this) : _y; set => _y = value; }
    public Vector2 Position => new Vector2(X, Y);

    public Func<Button, float>? GetX;
    public Func<Button, float>? GetY;

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

    public void Render(Renderer renderer)
    {
        if (Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
        {
            renderer.RenderFilledRectangle(Rectangle, Color.FromArgb(BackgroundColor.A, BackgroundColor.R + 4, BackgroundColor.G + 4, BackgroundColor.B + 4));
            renderer.RenderRectangle(Rectangle, Color.White);
        }
        else
        {
            renderer.RenderFilledRectangle(Rectangle, BackgroundColor);
        }

        if (Text != null)
        {
            renderer.RenderText(App.Font, Text, Vector2.Round(TextPosition), TextColor);
        }
    }
}