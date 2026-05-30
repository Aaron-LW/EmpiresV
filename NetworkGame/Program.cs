using System.Dynamic;
using Smash;
using Smash.Input;

internal static class Program
{
    public static bool TCPOnly { get; private set; } = false;

    private static void Main(string[] args)
    {
        if (args.Contains("--tcp-only"))
        {
            TCPOnly = true;
            Console.WriteLine("Enabling TCP-only mode");
        }

        if (args.Contains("--server"))
        {
            Console.WriteLine("Running as server");

            Server server = new Server();
            Task.Run(async () => server.Main());

            while (true) {}}
        
        else
        {
            Console.WriteLine("Running as client");

            SmashEngine.Init();
            InputHandler.StartPollingTextInput();

            App application = new App();
            application.Start();

            while (!application.ApplicationShouldClose())
            {
                SmashEngine.Update();

                application.Update(SmashEngine.DeltaTime);
                application.Render();
            };

            application.End();
            SmashEngine.Stop();
        }
    }
};
        

    

