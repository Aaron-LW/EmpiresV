using System.Text;

public class LeavePacket : Packet
{
    public string? Username;

    public LeavePacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_LEAVE) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_LEAVE}");
        Username = Encoding.UTF8.GetString(data[1..data.Length]);
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_LEAVE, ..Encoding.UTF8.GetBytes(Username!)];
    }
}