using System.Text;

public class PlayerDataPacket : Packet
{
    public PlayerData? PlayerData { get; set; }

    public PlayerDataPacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_PLAYERDATA) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_PLAYERDATA}");
        bool host = data[1] == 0x01;
        byte id = data[2];
        string username = Encoding.UTF8.GetString(data[3..(data.Length - 1)]);

        PlayerData = new()
        {
            Username = username,
            Host = host,
            Id = id
        };
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_PLAYERDATA, PlayerData!.Host ? (byte)0x01 : (byte)0x00, PlayerData.Id, ..Encoding.UTF8.GetBytes(PlayerData.Username)];
    }
}