using System.Text;

public class LoginPacket : Packet
{
    public string? Username { get; set; }

    public LoginPacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_LOGIN) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_LOGIN}");
        Username = Encoding.UTF8.GetString(data[1..data.Length]);
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_LOGIN, ..Encoding.UTF8.GetBytes(Username!)];
    }
}