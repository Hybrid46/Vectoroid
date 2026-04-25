namespace NetworkComponentSystem
{
    public interface INetworkComponent
    {
        void Encode(BinaryWriter bw);
        void Decode(BinaryReader br);
    }
}
