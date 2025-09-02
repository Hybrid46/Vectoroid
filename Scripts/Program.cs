using Raylib_cs;

namespace SpaceShooterMultiplayer
{
    internal static class Program
    {
        private const int WindowWidth = 1000;
        private const int WindowHeight = 700;

        private static void Main()
        {
            Raylib.SetConfigFlags(ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint);
            Raylib.InitWindow(WindowWidth, WindowHeight, "Space Shooter Multiplayer");
            Raylib.SetTargetFPS(60);

            var network = new NetworkManager();
            var game = new Game(network);

            double lastTime = Raylib.GetTime();
            double accumulator = 0.0;
            double frameTime = 1.0 / 60.0; // 60 FPS

            // Main loop
            while (!Raylib.WindowShouldClose())
            {
                double currentTime = Raylib.GetTime();
                double deltaTime = currentTime - lastTime;
                lastTime = currentTime;

                accumulator += deltaTime;

                while (accumulator >= frameTime)
                {
                    // Poll network before we update game objects
                    network.Poll();
                    game.Update();

                    accumulator -= frameTime;
                }

                game.Draw();
            }

            network.Disconnect();
            Raylib.CloseWindow();
        }
    }
}
