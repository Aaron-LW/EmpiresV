using System.Numerics;

public class PlayerData
{
    public float X { get; set; }
    public float Y { get; set; }
    public Vector2 Position => new Vector2(X, Y);
    
    public bool Host { get; set; }
}