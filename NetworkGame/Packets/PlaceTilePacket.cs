public class PlaceTilePacket : Packet
{
    public float X;
    public float Y;
    public int TextureId;

    public PlaceTilePacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_PLACE_TILE) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_PLACE_TILE}");
        X = BitConverter.ToSingle(data[1..5]);
        Y = BitConverter.ToSingle(data[5..9]);
        TextureId = BitConverter.ToInt32(data[9..13]);
    }
    
    public override byte[] Serialize()
    {
        return [PacketId.PACKET_PLACE_TILE, ..BitConverter.GetBytes(X), ..BitConverter.GetBytes(Y), ..BitConverter.GetBytes(TextureId)];
    }
}