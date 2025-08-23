using Raylib_cs;

namespace SpaceShooterMultiplayer
{
    internal static class Program
    {
        private const int WindowWidth = 1000;
        private const int WindowHeight = 700;

        private static void Main()
        {
            // Create the OpenGL context
            Raylib.SetConfigFlags(ConfigFlags.VSyncHint |
                                 ConfigFlags.Msaa4xHint);
            Raylib.InitWindow(WindowWidth, WindowHeight, "Space Shooter Multiplayer");
            Raylib.SetTargetFPS(60);

            var network = new NetworkManager();
            var game = new Game(network);

            // Main loop
            while (!Raylib.WindowShouldClose())
            {
                // Poll network *before* we update game objects
                network.Poll();

                game.Update();
                game.Draw();
            }

            network.Disconnect();
            Raylib.CloseWindow();
        }
    }
}
