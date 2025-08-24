using System.Numerics;

namespace SpaceShooterMultiplayer
{
    public class Star
    {
        public Vector2 Position { get; set; }
        public float Size { get; set; }

        public Star(Vector2 pos, float size)
        {
            Position = pos;
            Size = size;
        }
    }
}
