namespace NetworkComponentSystem
{
    internal class NetworkEntity
    {
        // unique per session
        public int Id;
        // the *real* entity (server) or the ghost (client)
        public Entity Local;

        public NetworkEntity(int id, Entity local) { Id = id; Local = local; }
    }
}
