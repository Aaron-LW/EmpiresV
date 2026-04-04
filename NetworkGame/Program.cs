using System.Runtime.InteropServices;
using System.Text;
using SDL3;
using Smash;
using Smash.Input;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.WriteLine("Running as server");

            Server server = new Server();
            Task.Run(async () => server.Main());

            while (true) { }
        }
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
            }

            application.End(); 
            SmashEngine.Stop();
        }

    }
}