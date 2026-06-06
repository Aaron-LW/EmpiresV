using System.Dynamic;
using Smash;
using Smash.Input;

internal static class Program
{
    public static bool TCPOnly { get; private set; } = false;

    private static void Main(string[] args)
    {
        if (args.Contains("--tcp-only") || args.Contains("-to"))
        {
            TCPOnly = true;
            Console.WriteLine("Enabling TCP-only mode");
        }

        if (args.Contains("--server") || args.Contains("-s"))
        {
            Console.WriteLine("Running as server");

            Server server = new Server();
            Task.Run(async () => server.Main());

            while (true) {}}
        
        else
        {
            Console.WriteLine("Running as client");

            SmashEngine.Init();

            App application = new App(args.Contains("--game") || args.Contains("-g"), args.Contains("--host") || args.Contains("-h"));
            application.Start();

            InputHandler.StartPollingTextInput();
            
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
        

    

