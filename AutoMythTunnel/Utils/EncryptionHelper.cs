using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoMythTunnel.Utils;

internal static partial class EncryptionHelper
{
    private static readonly ReadOnlyMemory<byte> SeqOid = new([0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00]);

    public static RSA? DecodePublicKey(byte[] publicKeyBytes)
    {
        MemoryStream ms = new(publicKeyBytes);
        BinaryReader rd = new(ms);
        byte[] seq = new byte[15];

        try
        {
            byte byteValue;
            ushort shortValue;

            shortValue = rd.ReadUInt16();

            switch (shortValue)
            {
                case 0x8130:
                    rd.ReadByte();

                    break;

                case 0x8230:
                    rd.ReadInt16();

                    break;

                default:
                    return null;
            }

            seq = rd.ReadBytes(15);

            if (!seq.AsSpan().SequenceEqual(SeqOid.Span))
            {
                return null;
            }

            shortValue = rd.ReadUInt16();

            if (shortValue == 0x8103)
            {
                rd.ReadByte();
            }
            else if (shortValue == 0x8203)
            {
                rd.ReadInt16();
            }
            else
            {
                return null;
            }

            byteValue = rd.ReadByte();

            if (byteValue != 0x00)
            {
                return null;
            }

            shortValue = rd.ReadUInt16();

            if (shortValue == 0x8130)
            {
                rd.ReadByte();
            }
            else if (shortValue == 0x8230)
            {
                rd.ReadInt16();
            }
            else
            {
                return null;
            }

            RSA rsa = RSA.Create();

            //RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(parms);
            RSAParameters rsaParams = new();

            rsaParams.Modulus = rd.ReadBytes(DecodeIntegerSize(rd));

            GetTraits(rsaParams.Modulus.Length * 8, out int sizeMod, out int sizeExp);

            rsaParams.Modulus = AlignBytes(rsaParams.Modulus, sizeMod);
            rsaParams.Exponent = AlignBytes(rd.ReadBytes(DecodeIntegerSize(rd)), sizeExp);

            rsa.ImportParameters(rsaParams);

            return rsa;
        }
        catch (Exception e)
        {
            return null;
        }
        finally
        {
            rd.Close();
        }
    }

    private static byte[] AlignBytes(byte[] inputBytes, int alignSize)
    {
        int inputBytesSize = inputBytes.Length;

        if (alignSize != -1 && inputBytesSize < alignSize)
        {
            byte[] buf = new byte[alignSize];

            inputBytes.CopyTo(buf.AsSpan().Slice(alignSize - inputBytesSize));

            return buf;
        }

        return inputBytes;
    }

    private static int DecodeIntegerSize(BinaryReader rd)
    {
        byte byteValue;
        int count;

        byteValue = rd.ReadByte();

        if (byteValue != 0x02)
        {
            return 0;
        }

        byteValue = rd.ReadByte();

        if (byteValue == 0x81)
        {
            count = rd.ReadByte();
        }
        else if (byteValue == 0x82)
        {
            byte hi = rd.ReadByte();
            byte lo = rd.ReadByte();
            count = BitConverter.ToUInt16(new[] { lo, hi }, 0);
        }
        else
        {
            count = byteValue;
        }

        while (rd.ReadByte() == 0x00)
        {
            count -= 1;
        }

        rd.BaseStream.Seek(-1, SeekOrigin.Current);

        return count;
    }

    private static void GetTraits(int modulusLengthInBits, out int sizeMod, out int sizeExp)
    {
        int assumedLength = -1;
        double logbase = Math.Log(modulusLengthInBits, 2);

        if (logbase == (int)logbase)
        {
            assumedLength = modulusLengthInBits;
        }
        else
        {
            assumedLength = (int)(logbase + 1.0);
            assumedLength = (int)Math.Pow(2, assumedLength);
            Debug.Assert(false);
        }

        switch (assumedLength)
        {
            case 512:
                sizeMod = 0x40;
                sizeExp = -1;

                break;

            case 1024:
                sizeMod = 0x80;
                sizeExp = -1;

                break;

            case 2048:
                sizeMod = 0x100;
                sizeExp = -1;

                break;

            case 4096:
                sizeMod = 0x200;
                sizeExp = -1;
                break;

            default:
                Debug.Assert(false);

                break;
        }

        sizeMod = -1;
        sizeExp = -1;
    }

    public static string PemKeyToDer(string pem)
    {
        Regex rx = PemKeyHeaderFooterRegex();
        string der = rx.Replace(pem, "")
                    .Replace("\r", "")
                    .Replace("\n", "");

        return der;
    }

    public static string ComputeHash(string serverId, byte[] key, byte[] publicKey)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(serverId)
                            .Concat(key)
                            .Concat(publicKey)
                            .ToArray();

        byte[] hash = SHA1.HashData(bytes);
        Array.Reverse(hash);
        BigInteger b = new(hash);

        string hex = b < 0
            ? "-" + (-b).ToString("x")
            : b.ToString("x");

        return hex.TrimStart('0');
    }

    [GeneratedRegex("-+[^-]+-+")]
    private static partial Regex PemKeyHeaderFooterRegex();
}