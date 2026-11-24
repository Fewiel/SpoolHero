using System.Text;

namespace SpoolManager.Infrastructure.Services;

/// <summary>
/// Extracts NDEF records from raw tag data. Accepts a bare NDEF message, an NDEF TLV
/// (0x03 ...) or a full NTAG21x memory dump (e.g. .bin files from the OpenSpool website).
/// </summary>
public static class NdefParser
{
    public sealed record NdefRecord(byte Tnf, string Type, byte[] Payload)
    {
        public string PayloadText => Encoding.UTF8.GetString(Payload);
    }

    /// <summary>Returns the first JSON payload found on the tag, or null.</summary>
    public static string? ExtractJson(byte[] data)
    {
        foreach (var record in ExtractRecords(data))
        {
            var isJsonType = record.Tnf == 0x02 && record.Type.Contains("json", StringComparison.OrdinalIgnoreCase);
            var text = record.PayloadText.Trim('\0', ' ', '\r', '\n', '\t');
            if (isJsonType || text.StartsWith('{'))
                return text;
        }
        return null;
    }

    public static List<NdefRecord> ExtractRecords(byte[] data)
    {
        if (data.Length == 0)
            return [];

        // 1) Bare NDEF message (first record has the Message Begin flag)
        if ((data[0] & 0x80) != 0 && TryParseMessage(data, 0, data.Length, out var bare) && bare.Count > 0)
            return bare;

        // 2) TLV stream starting at 0, or a tag dump where TLVs start at page 4 (byte 16)
        foreach (var start in new[] { 0, 16 })
        {
            if (TryParseTlv(data, start, out var records) && records.Count > 0)
                return records;
        }

        // 3) Last resort: scan for an NDEF TLV anywhere
        for (var i = 0; i < data.Length - 2; i++)
        {
            if (data[i] == 0x03 && TryParseTlv(data, i, out var records) && records.Count > 0)
                return records;
        }

        return [];
    }

    private static bool TryParseTlv(byte[] data, int pos, out List<NdefRecord> records)
    {
        records = [];
        while (pos < data.Length)
        {
            var tag = data[pos++];
            if (tag == 0x00)
                continue; // NULL TLV
            if (tag == 0xFE)
                return false; // terminator without NDEF TLV
            if (pos >= data.Length)
                return false;

            int length = data[pos++];
            if (length == 0xFF)
            {
                if (pos + 2 > data.Length)
                    return false;
                length = (data[pos] << 8) | data[pos + 1];
                pos += 2;
            }

            if (tag == 0x03)
            {
                if (pos + length > data.Length)
                    return false;
                return TryParseMessage(data, pos, pos + length, out records);
            }

            if (tag is not (0x01 or 0x02) && tag < 0xFD)
                return false; // unknown TLV – not a TLV stream
            pos += length;
        }
        return false;
    }

    private static bool TryParseMessage(byte[] data, int pos, int end, out List<NdefRecord> records)
    {
        records = [];
        while (pos < end)
        {
            var header = data[pos++];
            var tnf = (byte)(header & 0x07);
            var isShort = (header & 0x10) != 0;
            var hasId = (header & 0x08) != 0;
            var isLast = (header & 0x40) != 0;

            if (pos >= end)
                return false;
            int typeLength = data[pos++];

            long payloadLength;
            if (isShort)
            {
                if (pos >= end)
                    return false;
                payloadLength = data[pos++];
            }
            else
            {
                if (pos + 4 > end)
                    return false;
                payloadLength = ((long)data[pos] << 24) | ((long)data[pos + 1] << 16) | ((long)data[pos + 2] << 8) | data[pos + 3];
                pos += 4;
            }

            var idLength = 0;
            if (hasId)
            {
                if (pos >= end)
                    return false;
                idLength = data[pos++];
            }

            if (pos + typeLength + idLength + payloadLength > end)
                return false;

            var type = Encoding.ASCII.GetString(data, pos, typeLength);
            pos += typeLength + idLength;
            var payload = data.AsSpan(pos, (int)payloadLength).ToArray();
            pos += (int)payloadLength;
            records.Add(new NdefRecord(tnf, type, payload));

            if (isLast)
                return true;
        }
        return records.Count > 0;
    }
}
