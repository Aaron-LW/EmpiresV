using System.Numerics;

public class PositionComponent : Component
{
    public float X;
    public float Y;
    public Vector2 Position
    {
        get => new Vector2(X, Y);
        set { X = value.X; Y = value.Y; }
    }
}