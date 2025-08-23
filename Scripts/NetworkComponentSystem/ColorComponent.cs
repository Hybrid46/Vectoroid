using Raylib_cs;
using System.Numerics;

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
                    _color = value;
                    MarkDirty();
                }
            }
        }

        public static void Encode(BinaryWriter bw, ColorComponent c)
        {
            bw.Write(c._color.R);
            bw.Write(c._color.G);
            bw.Write(c._color.B);
            bw.Write(c._color.A);
        }

        public static void Decode(BinaryReader br, ColorComponent c)
        {
            c.Color = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }
    }
}
