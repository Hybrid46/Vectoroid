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
                    _position = value; Dirty = true;
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
                    _rotation = value; Dirty = true;
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
                    _scale = value; Dirty = true;
                }
            }
        }
    }
}