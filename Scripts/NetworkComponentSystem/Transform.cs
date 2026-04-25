using System.Numerics;

namespace NetworkComponentSystem
{
    public class Transform : Component, INetworkComponent
    {
        private Vector2 _position = Vector2.Zero;
        private float _rotation = 0f;
        private float _scale = 1f;

        public Transform()
        {
            componentType = ComponentType.Transform;
        }

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

        public Vector2 forward { get { return Forward(); } }

        public Vector2 Forward()
        {
            float rad = MathF.PI * Rotation / 180f;
            Vector2 dir = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
            return dir;
        }

        public void Encode(BinaryWriter bw)
        {
            bw.Write(Position.X);
            bw.Write(Position.Y);
            bw.Write(Rotation);
            bw.Write(Scale);
        }

        public void Decode(BinaryReader br)
        {
            Position = new Vector2(br.ReadSingle(), br.ReadSingle());
            Rotation = br.ReadSingle();
            Scale = br.ReadSingle();
        }
    }
}