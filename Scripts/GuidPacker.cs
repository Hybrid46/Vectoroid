public class GuidPacker
{
    public static (int a, int b, int c, int d) PackGuid(Guid guid)
    {
        byte[] bytes = guid.ToByteArray();

        int a = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
        int b = bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24);
        int c = bytes[8] | (bytes[9] << 8) | (bytes[10] << 16) | (bytes[11] << 24);
        int d = bytes[12] | (bytes[13] << 8) | (bytes[14] << 16) | (bytes[15] << 24);

        return (a, b, c, d);
    }

    public static Guid UnpackGuid(int a, int b, int c, int d)
    {
        byte[] bytes = new byte[16];

        bytes[0] = (byte)(a >> 0);
        bytes[1] = (byte)(a >> 8);
        bytes[2] = (byte)(a >> 16);
        bytes[3] = (byte)(a >> 24);

        bytes[4] = (byte)(b >> 0);
        bytes[5] = (byte)(b >> 8);
        bytes[6] = (byte)(b >> 16);
        bytes[7] = (byte)(b >> 24);

        bytes[8] = (byte)(c >> 0);
        bytes[9] = (byte)(c >> 8);
        bytes[10] = (byte)(c >> 16);
        bytes[11] = (byte)(c >> 24);

        bytes[12] = (byte)(d >> 0);
        bytes[13] = (byte)(d >> 8);
        bytes[14] = (byte)(d >> 16);
        bytes[15] = (byte)(d >> 24);

        return new Guid(bytes);
    }

    public static void Test()
    {
        // Generate a new GUID
        Guid originalGuid = Guid.NewGuid();
        Console.WriteLine($"Original GUID: {originalGuid}");

        // Pack the GUID into 4 integers
        var (a, b, c, d) = PackGuid(originalGuid);
        Console.WriteLine($"Packed integers: {a}, {b}, {c}, {d}");

        // Unpack the integers back to a GUID
        Guid unpackedGuid = UnpackGuid(a, b, c, d);
        Console.WriteLine($"Unpacked GUID: {unpackedGuid}");

        // Verify the round-trip integrity
        Console.WriteLine($"Round-trip successful: {originalGuid.Equals(unpackedGuid)}");
    }
}