namespace AutoMythTunnel.Proxy;

public class ProtocolVersion(int version)
{
    public static readonly List<int> SUPPORTED_VERSIONS = [47, 340];
    
    public int ParsePacketId(EnumPacketType enumPacketType)
    {
        int packetId = -1;
        switch (enumPacketType)
        {
            case EnumPacketType.CLIENT_CHAT:
                packetId = version switch
                {
                    47 => 0x01,
                    340 => 0x02,
                    _ => throw new Exception("Unsupported version")
                };
                break;
            case EnumPacketType.SERVER_CHAT:
                packetId = version switch
                {
                    47 => 0x02,
                    340 => 0x0f,
                    _ => throw new Exception("Unsupported version")
                };
                break;
            case EnumPacketType.SERVER_RESPAWN:
                packetId = version switch
                {
                    47 => 0x07,
                    340 => 0x35,
                    _ => throw new Exception("Unsupported version")
                };
                break;
            case EnumPacketType.SERVER_HEALTH_CHANGE:
                packetId = version switch
                {
                    47 => 0x06,
                    340 => 0x41,
                    _ => throw new Exception("Unsupported version")
                };
                break;
            case EnumPacketType.SERVER_SET_POSITION_AND_ROTATION:
                packetId = version switch
                {
                    47 => 0x08,
                    340 => 0x2f,
                    _ => throw new Exception("Unsupported version")
                };
                break;
            case EnumPacketType.SERVER_JOIN_GAME:
                packetId = version switch
                {
                    47 => 0x01,
                    340 => 0x23,
                    _ => throw new Exception("Unsupported version")
                };
                break;
            default:
                throw new Exception("Not supported packet type");
        }

        return packetId;
    }

    public EnumPacketType? ParsePacketType(EnumPacketWay way, int packetId)
    {
        if (way == EnumPacketWay.C2S)
        {
            return version switch
            {
                47 => packetId switch
                {
                    0x01 => EnumPacketType.CLIENT_CHAT,
                    0x02 => EnumPacketType.CLIENT_USE_ENTITY,
                    _ => null
                },
                340 => packetId switch
                {
                    0x02 => EnumPacketType.CLIENT_CHAT,
                    0x0a => EnumPacketType.CLIENT_USE_ENTITY,
                    _ => null
                },
                _ => null
            };
        }

        return version switch
        {
            47 => packetId switch
            {
                0x01 => EnumPacketType.SERVER_JOIN_GAME,
                0x02 => EnumPacketType.SERVER_CHAT,
                0x07 => EnumPacketType.SERVER_RESPAWN,
                0x06 => EnumPacketType.SERVER_HEALTH_CHANGE,
                0x08 => EnumPacketType.SERVER_SET_POSITION_AND_ROTATION,
                0x20 => EnumPacketType.SERVER_ENTITY_PROPERTIES,
                0x1c => EnumPacketType.SERVER_ENTITY_METADATA,
                0x0b => EnumPacketType.SERVER_ANIMATION,
                _ => null
            },
            340 => packetId switch
            {
                0x23 => EnumPacketType.SERVER_JOIN_GAME,
                0x0f => EnumPacketType.SERVER_CHAT,
                0x35 => EnumPacketType.SERVER_RESPAWN,
                0x41 => EnumPacketType.SERVER_HEALTH_CHANGE,
                0x2f => EnumPacketType.SERVER_SET_POSITION_AND_ROTATION,
                0x4e => EnumPacketType.SERVER_ENTITY_PROPERTIES,
                0x3c => EnumPacketType.SERVER_ENTITY_METADATA,
                0x06 => EnumPacketType.SERVER_ANIMATION,
                _ => null
            },
            _ => null
        };
    }
}