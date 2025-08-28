using Raylib_cs;
using System.Numerics;

namespace NetworkComponentSystem
{
    public class DrawComponent : Component
    {
        private int _typeID; // 0 = ship, 1 = bullet -> later it can be the texture ID for sprite drawing
        private Color _color = Color.White;

        public int TypeId
        {
            get => _typeID;
            set
            {
                if (_typeID != value)
                {
                    _typeID = value;
                    MarkDirty();
                }
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                if (_color.R != value.R ||
                    _color.G != value.G ||
                    _color.B != value.B ||
                    _color.A != value.A)
                {
                    _color = value;
                    MarkDirty();
                }
            }
        }

        public DrawComponent(int typeId, Color color)
        {
            TypeId = typeId;
            Color = color;
        }

        public void Draw()
        {
            switch (_typeID)
            {
                case 0: DrawShip(); break;
                case 1: DrawBullet(); break;
                default: break;
            }
        }

        public static void Encode(BinaryWriter bw, DrawComponent d)
        {
            bw.Write(d.TypeId);
            bw.Write((float)d._color.R);
            bw.Write((float)d._color.G);
            bw.Write((float)d._color.B);
            bw.Write((float)d._color.A);
        }

        public static void Decode(BinaryReader br, DrawComponent d)
        {
            d.TypeId = br.ReadInt32();
            d.Color = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        private void DrawBullet()
        {
            Raylib.DrawCircleV(Entity.transform.Position, 4, Color);
        }

        private void DrawShip()
        {
            Transform transform = Entity.transform;
            float rad = MathF.PI * transform.Rotation / 180f;

            // Calculate forward vector (unit vector) - already in Raylib's coordinate system
            Vector2 forward = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));

            // Calculate nose (30 units in forward direction)
            Vector2 nose = forward * 30f;

            // Calculate left (20 units at 150 degrees from forward)
            Vector2 left = new Vector2(
                forward.X * MathF.Cos(5f * MathF.PI / 6f) - forward.Y * MathF.Sin(5f * MathF.PI / 6f),
                forward.X * MathF.Sin(5f * MathF.PI / 6f) + forward.Y * MathF.Cos(5f * MathF.PI / 6f)
            ) * 20f;

            // Calculate right (20 units at -150 degrees from forward)
            Vector2 right = new Vector2(
                forward.X * MathF.Cos(-5f * MathF.PI / 6f) - forward.Y * MathF.Sin(-5f * MathF.PI / 6f),
                forward.X * MathF.Sin(-5f * MathF.PI / 6f) + forward.Y * MathF.Cos(-5f * MathF.PI / 6f)
            ) * 20f;

            // Convert to absolute coordinates by adding player position
            Vector2 nosePoint = transform.Position + nose;
            Vector2 leftPoint = transform.Position + left;
            Vector2 rightPoint = transform.Position + right;

            // Draw triangle in counter-clockwise order (nose -> right -> left)
            Raylib.DrawTriangle(nosePoint, rightPoint, leftPoint, Color);

            // Thrust effects (flame)
            //if (IsThrusting)
            {
                Vector2 backward = -forward; // backward direction

                float angle = MathF.PI / 6f; // 30 degrees

                // Left flame: rotate backward by +30 degrees (counterclockwise)
                Vector2 flameL = new Vector2(
                    backward.X * MathF.Cos(angle) - backward.Y * MathF.Sin(angle),
                    backward.X * MathF.Sin(angle) + backward.Y * MathF.Cos(angle)
                ) * 15f;

                // Right flame: rotate backward by -30 degrees (clockwise)
                Vector2 flameR = new Vector2(
                    backward.X * MathF.Cos(angle) + backward.Y * MathF.Sin(angle),
                    -backward.X * MathF.Sin(angle) + backward.Y * MathF.Cos(angle)
                ) * 15f;

                Vector2 tailPoint = transform.Position + backward * 20f;
                Vector2 flameLPoint = transform.Position + flameL;
                Vector2 flameRPoint = transform.Position + flameR;

                // Draw the flame triangle: tail, right, left (to match the main ship's winding order)
                Raylib.DrawTriangle(tailPoint, flameRPoint, flameLPoint, Color.Orange);
            }
        }
    }
}
