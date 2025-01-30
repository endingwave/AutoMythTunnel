using System.Net.Sockets;
using System.Runtime.Serialization;
using ICSharpCode.SharpZipLib.Zip.Compression;
using MineSharp.Core.Common;
using MineSharp.Core.Serialization;
using MineSharp.Protocol.Cryptography;
using NLog;

namespace AutoMythTunnel.Proxy;

/// <summary>
/// Handles reading and writing packets.
/// Also handles encryption and compression.
/// This class is thread-safe.
/// </summary>
public class MinecraftStream
{
    private const int CompressionDisabled = -1;
    private int compressionThreshold = CompressionDisabled;

    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly Deflater deflater = new();
    private readonly Inflater inflater = new();
    private readonly NetworkStream networkStream;
    
    private object writeLock = new();

    public int protocolVersion;
    private AesStream? encryptionStream;

    public Stream stream;

    public MinecraftStream(NetworkStream networkStream, int protocolVersion)
    {
        this.protocolVersion = protocolVersion;
        this.networkStream = networkStream;
        stream = this.networkStream;
    }

    public void EnableEncryption(byte[] sharedSecret)
    {
        Logger.Debug("Enabling encryption.");
        encryptionStream = new AesStream(networkStream, sharedSecret);
        stream = encryptionStream;
    }

    public PacketBuffer ReadPacket()
    {
        int length = ReadVarInt(out _);

        byte[] data = new byte[length];
            
        int read = 0;
        while (read < length)
        {
            read += stream.Read(data, read, length - read);
        }

        PacketBuffer _rawBuffer = new(data, protocolVersion);
        int localCompressionThreshold = compressionThreshold;
        int uncompressedLength = 0;
        
        int ra = 0;
        if (localCompressionThreshold != CompressionDisabled)
        {
            uncompressedLength = ReadVarInt(_rawBuffer, out int r);
            ra = r;
        }

        return uncompressedLength switch
        {
            > 0 => DecompressBuffer(_rawBuffer.GetBuffer().Skip(ra).ToArray(), uncompressedLength),
            _ => new PacketBuffer(
                localCompressionThreshold != CompressionDisabled
                    ? _rawBuffer.GetBuffer().Skip(ra).ToArray()
                    : _rawBuffer.GetBuffer(), protocolVersion)
        };
    }

    public void WritePacket(PacketBuffer buffer)
    {
        lock (writeLock)
        {
            if (compressionThreshold > 0)
            {
                buffer = CompressBuffer(buffer);
            }
            
            WriteVarInt(buffer.GetBuffer().Length);
            stream.Write(buffer.GetBuffer().AsSpan());
        }
    }
    
    public void SetCompressionThreshold(int threshold)
    {
        compressionThreshold = threshold;
    }
    
    public void WriteRawPacket(PacketBuffer buffer)
    {
        stream.Write(buffer.GetBuffer().AsSpan());
    }

    private int ReadVarInt(out int read)
    {
        int value = 0;
        int shift = 0;
        int byteCount = 0;

        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                throw new EndOfStreamException();
            }

            byteCount++;
            value |= (b &  0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                break;
            }

            shift += 7;
            if (shift >= 32)
            {
                throw new SerializationException("VarInt is too big.");
            }
        }

        read = value;
        return value;
    }
    
    private void WriteVarInt(int value)
    {
        while (true)
        {
            if ((value & ~0x7F) == 0)
            {
                stream.WriteByte((byte)value);
                return;
            }

            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
    }
    
    private int ReadVarInt(PacketBuffer buffer, out int read)
    {
        int value = 0;
        int length = 0;
        byte currentByte;

        while (true)
        {
            currentByte = (byte)buffer.ReadByte();
            value |= (currentByte & 0x7F) << (length * 7);

            length++;
            if (length > 5)
            {
                throw new("VarInt too big");
            }

            if ((currentByte & 0x80) != 0x80)
            {
                break;
            }
        }

        read = length;
        return value;
    }

    private void WriteVarInt(PacketBuffer buffer, int value)
    {
        while (true)
        {
            if ((value & ~0x7F) == 0)
            {
                buffer.WriteByte((byte)value);
                return;
            }

            buffer.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
    }

    public void Close()
    {
        networkStream.Close();
        encryptionStream?.Close();
    }
    
    private PacketBuffer DecompressBuffer(byte[] buffer, int length)
    {
        if (length == 0)
        {
            return new(buffer, protocolVersion);
        }
        
        byte[] buffer2 = new byte[length];
        inflater.SetInput(buffer);
        inflater.Inflate(buffer2);
        inflater.Reset();

        return new(buffer2, protocolVersion);
    }

    private PacketBuffer CompressBuffer(PacketBuffer input)
    {
        PacketBuffer output = new(protocolVersion);
        if (input.Size < compressionThreshold)
        {
            output.WriteVarInt(0);
            output.WriteBytes(input.GetBuffer().AsSpan());
            return output;
        }

        byte[] buffer = input.GetBuffer();
        output.WriteVarInt(buffer.Length);

        deflater.SetInput(buffer);
        deflater.Finish();

        byte[] deflateBuf = new byte[8192];
        while (!deflater.IsFinished)
        {
            int j = deflater.Deflate(deflateBuf);
            output.WriteBytes(deflateBuf.AsSpan(0, j));
        }

        deflater.Reset();
        return output;
    }
}