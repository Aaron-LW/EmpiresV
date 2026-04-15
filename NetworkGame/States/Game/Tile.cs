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

        Texture2D textureAtlas = AssetManager.GetTexture("TextureAtlas");

        TexX = texture.SourceRectangle.X / (float)textureAtlas.Width;
        TexY = texture.SourceRectangle.Y / (float)textureAtlas.Height;
    }
}