public class UpdateHostPacket : Packet
{
    public byte NewHostPlayerId;

    public UpdateHostPacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_UPDATE_HOST) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_UPDATE_HOST}");
        NewHostPlayerId = data[1];
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_UPDATE_HOST, NewHostPlayerId];
    }
}