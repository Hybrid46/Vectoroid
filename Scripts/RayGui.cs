// RayGui.cs
using Raylib_cs;

namespace SpaceShooterMultiplayer
{
    public static class RayGui
    {
        public static bool GuiButton(Rectangle bounds, string text)
        {
            Color baseCol = new Color(50, 100, 150, 255);
            Color hoverCol = new Color(70, 120, 180, 255);
            Color pressCol = new Color(30, 80, 130, 255);

            bool hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);
            bool pressed = hovered && Raylib.IsMouseButtonDown(MouseButton.Left);
            bool clicked = hovered && Raylib.IsMouseButtonReleased(MouseButton.Left);

            Color col = pressed ? pressCol : (hovered ? hoverCol : baseCol);

            Raylib.DrawRectangleRec(bounds, col);
            Raylib.DrawRectangleLinesEx(bounds, 2, Color.White);
            Raylib.DrawText(text,
                (int)(bounds.X + bounds.Width / 2 - Raylib.MeasureText(text, 20) / 2),
                (int)(bounds.Y + bounds.Height / 2 - 10),
                20, Color.White);

            return clicked;
        }

        public static string GuiTextBox(Rectangle bounds, string txt, int maxLen, bool edit)
        {
            Color baseCol = new Color(30, 30, 30, 255);
            Color activeCol = new Color(40, 40, 40, 255);

            bool active = edit;
            Color col = active ? activeCol : baseCol;

            Raylib.DrawRectangleRec(bounds, col);
            Raylib.DrawRectangleLinesEx(bounds, 2, Color.White);
            Raylib.DrawText(txt, (int)bounds.X + 5, (int)bounds.Y + 5, 20, Color.White);

            if (active && (Raylib.GetTime() * 4 % 1) < 0.5f)
                Raylib.DrawLine(
                    (int)(bounds.X + 5 + Raylib.MeasureText(txt, 20)),
                    (int)(bounds.Y + 5),
                    (int)(bounds.X + 5 + Raylib.MeasureText(txt, 20)),
                    (int)(bounds.Y + bounds.Height - 5),
                    Color.White);

            return txt;
        }
    }
}
