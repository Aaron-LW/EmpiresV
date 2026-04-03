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

            App application = new App();
            application.Start();

            bool running = true;
            while (running)
            {
                SmashEngine.Update(false);

                while (SDL.PollEvent(out SDL.Event e))
                {
                    if (e.Type == (uint)SDL.EventType.Quit)
                    {
                        running = false;
                    }

                    if (e.Type == (uint)SDL.EventType.TextInput)
                    {
                        application.SendTextInput(Marshal.PtrToStringUTF8(e.Text.Text));
                    }

                    InputHandler.Event(e);
                }

                application.Update(SmashEngine.DeltaTime);
                application.Render();
            }

            application.End(); 
            SmashEngine.Stop();
        }

    }
}