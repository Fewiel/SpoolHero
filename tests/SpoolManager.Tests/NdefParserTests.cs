using System.Text;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Shared.Models;

namespace SpoolManager.Tests;

public class NdefParserTests
{
    private const string Json = """{"protocol":"openspool","version":"1.0","type":"PLA","color_hex":"FF0000","brand":"Generic","min_temp":190,"max_temp":220}""";

    private static byte[] Record(string type, string payload, bool messageBegin = true, bool messageEnd = true, byte[]? id = null)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var shortRecord = payloadBytes.Length <= 255;
        var header = (byte)((messageBegin ? 0x80 : 0) | (messageEnd ? 0x40 : 0) | (shortRecord ? 0x10 : 0) | (id != null ? 0x08 : 0) | 0x02);
        var bytes = new List<byte> { header, (byte)typeBytes.Length };
        if (shortRecord)
            bytes.Add((byte)payloadBytes.Length);
        else
            bytes.AddRange([(byte)(payloadBytes.Length >> 24), (byte)(payloadBytes.Length >> 16), (byte)(payloadBytes.Length >> 8), (byte)payloadBytes.Length]);
        if (id != null)
            bytes.Add((byte)id.Length);
        bytes.AddRange(typeBytes);
        if (id != null)
            bytes.AddRange(id);
        bytes.AddRange(payloadBytes);
        return [.. bytes];
    }

    private static byte[] Tlv(byte[] message)
    {
        var bytes = new List<byte> { 0x03 };
        if (message.Length < 0xFF)
            bytes.Add((byte)message.Length);
        else
            bytes.AddRange([0xFF, (byte)(message.Length >> 8), (byte)message.Length]);
        bytes.AddRange(message);
        bytes.Add(0xFE);
        return [.. bytes];
    }

    [Fact]
    public void ExtractsJsonFromBareRecord()
    {
        Assert.Equal(Json, NdefParser.ExtractJson(Record("application/json", Json)));
    }

    [Fact]
    public void ExtractsJsonFromTlv()
    {
        Assert.Equal(Json, NdefParser.ExtractJson(Tlv(Record("application/json", Json))));
    }

    [Fact]
    public void ExtractsJsonFromNtag215Dump()
    {
        // 540 byte NTAG215 image: pages 0-3 = UID/lock/capability container, NDEF TLV from page 4
        var dump = new byte[540];
        byte[] header = [0x04, 0x11, 0x22, 0xB7, 0x33, 0x44, 0x55, 0x66, 0x77, 0x48, 0x00, 0x00, 0xE1, 0x10, 0x3E, 0x00];
        header.CopyTo(dump, 0);
        Tlv(Record("application/json", Json)).CopyTo(dump, 16);

        Assert.Equal(Json, NdefParser.ExtractJson(dump));
    }

    [Fact]
    public void ExtractsJsonFromDumpWithLockControlTlv()
    {
        var dump = new byte[540];
        new byte[] { 0x04, 0x11, 0x22, 0xB7, 0x33, 0x44, 0x55, 0x66, 0x77, 0x48, 0x00, 0x00, 0xE1, 0x10, 0x3E, 0x00 }.CopyTo(dump, 0);
        byte[] lockControl = [0x01, 0x03, 0xA0, 0x0C, 0x34];
        lockControl.CopyTo(dump, 16);
        Tlv(Record("application/json", Json)).CopyTo(dump, 16 + lockControl.Length);

        Assert.Equal(Json, NdefParser.ExtractJson(dump));
    }

    [Fact]
    public void FindsJsonRecordInMultiRecordMessage()
    {
        var first = Record("application/json", Json, messageBegin: true, messageEnd: false);
        var second = Record("text/plain", "SPOOL:42", messageBegin: false, messageEnd: true, id: [0x01]);
        var records = NdefParser.ExtractRecords([.. first, .. second]);

        Assert.Equal(2, records.Count);
        Assert.Equal("SPOOL:42", records[1].PayloadText);
        Assert.Equal(Json, NdefParser.ExtractJson([.. first, .. second]));
    }

    [Fact]
    public void HandlesLongRecords()
    {
        var longJson = "{\"protocol\":\"openspool\",\"note\":\"" + new string('x', 400) + "\"}";
        Assert.Equal(longJson, NdefParser.ExtractJson(Tlv(Record("application/json", longJson))));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x01, 0x02, 0x03 })]
    [InlineData(new byte[] { 0xD2, 0x10, 0xFF, 0x00 })]
    public void ReturnsNullForGarbage(byte[] data)
    {
        Assert.Null(NdefParser.ExtractJson(data));
    }

    [Fact]
    public void RoundTripsOpenSpoolServiceEncoding()
    {
        var service = new OpenSpoolService();
        var material = new FilamentMaterial { Brand = "Generic", Type = "PETG", ColorHex = "00FF00", MinTempCelsius = 220, MaxTempCelsius = 250 };
        var spoolId = Guid.NewGuid();

        var (decoded, isValid) = service.Decode(service.Encode(material, spoolId));

        Assert.True(isValid);
        Assert.Equal("PETG", decoded.Type);
        Assert.Equal("Generic", decoded.Brand);
        Assert.Equal(220, decoded.MinTempCelsius);
    }

    [Fact]
    public void ParsesTwoRecordTagWithSpoolmanText()
    {
        var service = new OpenSpoolService();
        var material = new FilamentMaterial { Brand = "Generic", Type = "PLA", ColorHex = "FF0000", MinTempCelsius = 190, MaxTempCelsius = 220 };

        var bytes = service.Encode(material, Guid.NewGuid(), spoolmanId: 42);
        var records = NdefParser.ExtractRecords(bytes);

        Assert.Equal(2, records.Count);
        Assert.Equal("T", records[1].Type);
        Assert.Contains("SPOOL:42", records[1].PayloadText);
        Assert.True(service.Decode(bytes).isValid);
        Assert.Contains("\"spool_id\":42", NdefParser.ExtractJson(bytes));
    }
}
