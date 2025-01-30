using MineSharp.Core.Serialization;
using MineSharp.Data;
using MineSharp.Protocol.Packets.NetworkTypes;

namespace AutoMythTunnel.Utils;

public static class ExtraEntityMetadata
{
    public static EntityMetadata ReadEntityMetadata(PacketBuffer buffer, MinecraftData data)
    {
        List<EntityMetadataEntry> entries = new List<EntityMetadataEntry>();
        while (true)
        {
            // TODO: Some metadata is broken and causes exceptions
            byte index = buffer.Peek();
            if (index == 0xff)
            {
                buffer.ReadByte(); // Consume the index
                break;
            }

            EntityMetadataEntry entry = ReadEntityMetadataEntry(buffer, data, index);
            entries.Add(entry);
        }
        return new EntityMetadata(entries.ToArray());
    }
    
    public static EntityMetadataEntry ReadEntityMetadataEntry(PacketBuffer buffer, MinecraftData data, byte originIndex)
    {
        int index = (originIndex & 224) >> 5;
        int type = originIndex & 31;
        IMetadataValue value = MetadataValueFactory.Create(type, buffer, data);

        return new EntityMetadataEntry((byte)index, type, value);
    }

    public static string ReadUTF(this PacketBuffer buffer)
    {
        int utflen = buffer.ReadShort() & 0xFFFF;
        byte[] bytearr;
        char[] chararr = new char[utflen];
        int c, char2, char3;
        int count = 0;
        int chararr_count = 0;

        bytearr = buffer.ReadBytes(utflen);

        while (count < utflen)
        {
            c = bytearr[count] & 0xff;
            if (c > 127) break;
            count++;
            chararr[chararr_count++] = (char)c;
        }

        while (count < utflen)
        {
            c = bytearr[count] & 0xff;
            switch (c >> 4)
            {
                case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7:
                    /* 0xxxxxxx*/
                    count++;
                    chararr[chararr_count++] = (char)c;
                    break;
                case 12: case 13:
                    /* 110x xxxx   10xx xxxx*/
                    count += 2;
                    if (count > utflen)
                        throw new Exception(
                            "malformed input: partial character at end");
                    char2 = bytearr[count - 1];
                    if ((char2 & 0xC0) != 0x80)
                        throw new Exception(
                            "malformed input around byte " + count);
                    chararr[chararr_count++] = (char)(((c & 0x1F) << 6) |
                                                      (char2 & 0x3F));
                    break;
                case 14:
                    /* 1110 xxxx  10xx xxxx  10xx xxxx */
                    count += 3;
                    if (count > utflen)
                        throw new Exception(
                            "malformed input: partial character at end");
                    char2 = bytearr[count - 2];
                    char3 = bytearr[count - 1];
                    if (((char2 & 0xC0) != 0x80) || ((char3 & 0xC0) != 0x80))
                        throw new Exception(
                            "malformed input around byte " + (count - 1));
                    chararr[chararr_count++] = (char)(((c & 0x0F) << 12) |
                                                      ((char2 & 0x3F) << 6) |
                                                      ((char3 & 0x3F) << 0));
                    break;
                default:
                    /* 10xx xxxx,  1111 xxxx */
                    throw new Exception(
                        "malformed input around byte " + count);
            }
        }
        // The number of chars produced may be less than utflen
        return new string(chararr, 0, chararr_count);
    }

    public static void SkipNbt(this PacketBuffer buffer, int type)
    {
        switch (type)
        {
            case 1:
                buffer.ReadByte();
                break;
            case 2:
                buffer.ReadShort();
                return;
            case 3:
                buffer.ReadInt();
                break;
            case 4:
                buffer.ReadLong();
                break;
            case 5:
                buffer.ReadFloat();
                break;
            case 6:
                buffer.ReadDouble();
                break;
            case 7:
                buffer.ReadBytes(buffer.ReadInt());
                break;
            case 8:
                buffer.ReadUTF();
                break;
            case 9:
                int listType = buffer.ReadByte();
                int length = buffer.ReadInt();
                for (int i = 0; i < length; i++)
                {
                    buffer.SkipNbt(listType);
                }
                break;
            case 10:
                int tagType;
                while (true)
                {
                    tagType = buffer.ReadByte();
                    if (tagType == 0) break;
                    buffer.ReadUTF();
                    buffer.SkipNbt(tagType);
                }
                break;
            case 11:
                int length2 = buffer.ReadInt();
                for (int i = 0; i < length2; i++)
                {
                    buffer.ReadInt();
                }
                break;
        }
    }
}