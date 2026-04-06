using System.Drawing;
using System.Numerics;

public class Notification
{
    public required string Message;
    public required NotificationLevel NotificationLevel;

    public float X;
    public float Y;
    public Vector2 Position => new Vector2(X, Y);

    public float PreferredX;
    public float PreferredY;
    public Vector2 PreferredPosition => new Vector2(PreferredX, PreferredY);
}