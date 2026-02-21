using TotpManager.Core.Models;

namespace TotpManager.Core;

/// <summary>
/// Hand-rolled proto3 wire-format decoder. No Grpc.Tools dependency needed.
/// Wire type 0 = varint, wire type 2 = length-delimited (bytes/string/embedded message).
/// </summary>
public static class ProtobufDecoder
{
    // Field numbers for MigrationPayload
    // 1 = otp_parameters (embedded message, repeated)
    // 2 = version (varint)
    // 3 = batch_size (varint)
    // 4 = batch_index (varint)
    // 5 = batch_id (varint)

    // Field numbers for OtpParameters
    // 1 = secret (bytes)
    // 2 = name (string)
    // 3 = issuer (string)
    // 4 = algorithm (varint/enum)
    // 5 = digits (varint/enum)
    // 6 = type (varint/enum)
    // 7 = counter (varint)
    // 8 = id (varint)

    public static MigrationPayload DecodePayload(byte[] data)
    {
        var payload = new MigrationPayload();
        var reader = new ProtoReader(data);

        while (reader.HasMore)
        {
            var (fieldNumber, wireType) = reader.ReadTag();
            switch (fieldNumber)
            {
                case 1 when wireType == 2:
                    var otpBytes = reader.ReadLengthDelimited();
                    payload.OtpParameters.Add(DecodeOtpParameters(otpBytes));
                    break;
                case 2 when wireType == 0:
                    payload.Version = (int)reader.ReadVarint();
                    break;
                case 3 when wireType == 0:
                    payload.BatchSize = (int)reader.ReadVarint();
                    break;
                case 4 when wireType == 0:
                    payload.BatchIndex = (int)reader.ReadVarint();
                    break;
                case 5 when wireType == 0:
                    payload.BatchId = (int)reader.ReadVarint();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return payload;
    }

    public static OtpParameters DecodeOtpParameters(byte[] data)
    {
        var otp = new OtpParameters();
        var reader = new ProtoReader(data);

        while (reader.HasMore)
        {
            var (fieldNumber, wireType) = reader.ReadTag();
            switch (fieldNumber)
            {
                case 1 when wireType == 2:
                    otp.Secret = reader.ReadLengthDelimited();
                    break;
                case 2 when wireType == 2:
                    otp.Name = reader.ReadString();
                    break;
                case 3 when wireType == 2:
                    otp.Issuer = reader.ReadString();
                    break;
                case 4 when wireType == 0:
                    otp.Algorithm = (Algorithm)(int)reader.ReadVarint();
                    break;
                case 5 when wireType == 0:
                    otp.Digits = (DigitCount)(int)reader.ReadVarint();
                    break;
                case 6 when wireType == 0:
                    otp.Type = (OtpType)(int)reader.ReadVarint();
                    break;
                case 7 when wireType == 0:
                    otp.Counter = (long)reader.ReadVarint();
                    break;
                case 8 when wireType == 0:
                    otp.Id = (int)reader.ReadVarint();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return otp;
    }

    private ref struct ProtoReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;

        public ProtoReader(byte[] data)
        {
            _data = data;
            _pos = 0;
        }

        public readonly bool HasMore => _pos < _data.Length;

        public (int fieldNumber, int wireType) ReadTag()
        {
            ulong tag = ReadVarint();
            return ((int)(tag >> 3), (int)(tag & 0x7));
        }

        public ulong ReadVarint()
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                byte b = _data[_pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift >= 64) throw new InvalidDataException("Varint too long.");
            }
            return result;
        }

        public byte[] ReadLengthDelimited()
        {
            int length = (int)ReadVarint();
            var bytes = _data.Slice(_pos, length).ToArray();
            _pos += length;
            return bytes;
        }

        public string ReadString()
        {
            var bytes = ReadLengthDelimited();
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public void SkipField(int wireType)
        {
            switch (wireType)
            {
                case 0: // varint
                    ReadVarint();
                    break;
                case 1: // 64-bit
                    _pos += 8;
                    break;
                case 2: // length-delimited
                    int length = (int)ReadVarint();
                    _pos += length;
                    break;
                case 5: // 32-bit
                    _pos += 4;
                    break;
                default:
                    throw new InvalidDataException($"Unknown wire type: {wireType}");
            }
        }
    }
}
