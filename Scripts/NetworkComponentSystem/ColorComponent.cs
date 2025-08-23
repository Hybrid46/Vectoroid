using Raylib_cs;

namespace NetworkComponentSystem
{
    public class ColorComponent : Component
    {
        private Color _color = Color.White;

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
                    _color = value; Dirty = true;
                }
            }
        }
    }
}
