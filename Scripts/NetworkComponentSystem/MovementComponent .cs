using System.Numerics;

namespace NetworkComponentSystem
{
    public class MovementComponent : Component, IUpdatable
    {
        private Vector2 _direction;
        private float _speed;
        private float _acceleration;
        private float _maxSpeed;
        private float _minSpeed;
        private bool _shouldDecayWhenZeroSpeed;

        public MovementComponent(Vector2 direction, float initialSpeed, float acceleration = 0f,
                                float maxSpeed = float.MaxValue, float minSpeed = 0f,
                                bool shouldDecay = false)
        {
            _direction = direction;
            _speed = initialSpeed;
            _acceleration = acceleration;
            _maxSpeed = maxSpeed;
            _minSpeed = minSpeed;
            _shouldDecayWhenZeroSpeed = shouldDecay;
        }

        public void Update()
        {
            _speed += _acceleration;
            _speed = Math.Clamp(_speed, _minSpeed, _maxSpeed);

            Vector2 movement = _direction * _speed;

            // Apply movement to transform
            if (Entity.transform != null)
            {
                Entity.transform.Position += movement;
            }

            // Decay to zero and destroy when minimum speed is reached
            if (_shouldDecayWhenZeroSpeed && Math.Abs(_speed) < float.Epsilon)
            {
                Entity.Destroy();
            }

            MarkDirty();
        }

        public static void Encode(BinaryWriter bw, MovementComponent m)
        {
            bw.Write(m._direction.X);
            bw.Write(m._direction.Y);
            bw.Write(m._speed);
            bw.Write(m._acceleration);
            bw.Write(m._maxSpeed);
            bw.Write(m._minSpeed);
            bw.Write(m._shouldDecayWhenZeroSpeed);
        }

        public static void Decode(BinaryReader br, MovementComponent m)
        {
            m._direction = new Vector2(br.ReadSingle(), br.ReadSingle());
            m._speed = br.ReadSingle();
            m._acceleration = br.ReadSingle();
            m._maxSpeed = br.ReadSingle();
            m._minSpeed = br.ReadSingle();
            m._shouldDecayWhenZeroSpeed = br.ReadBoolean();
        }
    }
}