using Smash;
using Smash.Graphics;

public static class NotificationManager
{
    public static Window? Window;

    private const int NOTIFICATION_HEIGHT = 50;
    private const int PADDING = 20;
    private const int ANIMATION_SPEED = 40;

    private static List<Notification> _notifications = new();

    public static void Notify(string notification, NotificationLevel notificationLevel)
    {
        if (Window == null) return;

        if (_notifications.Count == 0)
        {
            _notifications.Add(new Notification()
            {
                Message = notification,
                NotificationLevel = notificationLevel,
                X = Window.Width,
                Y = Window.Height - NOTIFICATION_HEIGHT - PADDING,
                PreferredX = Window.Width - PADDING,
                PreferredY = Window.Height - NOTIFICATION_HEIGHT - PADDING
            });
        }
    }

    public static void Update(double deltaTime)
    {
        foreach (Notification notification in _notifications)
        {
            notification.X = MathHelper.Lerp(notification.X, notification.PreferredX, ANIMATION_SPEED * (float)deltaTime);
            notification.Y = MathHelper.Lerp(notification.Y, notification.PreferredY, ANIMATION_SPEED * (float)deltaTime);
        }
    }

    public static void Render(Renderer renderer)
    {
        //foreach (Notification notification in _notifications)
        //{
        //    Color color = Color.White;

        //    switch (notification.NotificationLevel)
        //    {
        //        case NotificationLevel.Normal:
        //            color = Color.RoyalBlue;
        //            break;

        //        case NotificationLevel.Warning:
        //            color = Color.Yellow;
        //            break;

        //        case NotificationLevel.Error:
        //            color = Color.DarkRed;
        //            break;
        //    }

        //    renderer.RenderFilledRectangle(new Rectangle(notification.Position - App.Font.MeasureString(notification.Message) - new Vector2(PADDING), App.Font.MeasureString(notification.Message).X + PADDING * 2, NOTIFICATION_HEIGHT), color);
        //    renderer.RenderText(App.Font, notification.Message, notification.Position - App.Font.MeasureString(notification.Message), Color.White);
        //}
    }
}