using Smash.Graphics;

public class Tile
{
    public int X;
    public int Y;
    public Texture2D Texture;

    public float Brightness;

    public readonly float TexX;
    public readonly float TexY;

    public Tile(int x, int y, Texture2D texture, float brightness)
    {
        X = x;
        Y = y;
        Texture = texture;
        Brightness = brightness;

        TexX = texture.SourceRectangle.X / 32f;
        TexY = texture.SourceRectangle.Y / 32f;
    }
}