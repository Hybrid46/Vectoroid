using System.Numerics;

namespace NetworkComponentSystem
{
    public class Transform : Component
    {
        private Vector2 _position = Vector2.Zero;
        private float _rotation = 0f;
        private float _scale = 1f;

        public Vector2 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    MarkDirty();
                }
            }
        }

        public float Rotation
        {
            get => _rotation;
            set
            {
                if (_rotation != value)
                {
                    _rotation = value;
                    MarkDirty();
                }
            }
        }

        public float Scale
        {
            get => _scale;
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    MarkDirty();
                }
            }
        }

        public static void Encode(BinaryWriter bw, Transform t)
        {
            bw.Write(t.Position.X);
            bw.Write(t.Position.Y);
            bw.Write(t.Rotation);
            bw.Write(t.Scale);
        }

        public static void Decode(BinaryReader br, Transform t)
        {
            t.Position = new Vector2(br.ReadSingle(), br.ReadSingle());
            t.Rotation = br.ReadSingle();
            t.Scale = br.ReadSingle();
        }
    }
}