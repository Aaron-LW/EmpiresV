using Smash.Graphics;

public class TextureComponent : Component
{
    public Texture2D Texture;
    public float Scale = 1;

    public TextureComponent(Texture2D texture)
    {
        Texture = texture;
    }
}