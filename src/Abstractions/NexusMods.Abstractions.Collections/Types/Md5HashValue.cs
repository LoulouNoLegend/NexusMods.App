using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransparentValueObjects;

namespace NexusMods.Abstractions.Collections.Types;

/// <summary>
/// A value object representing an MD5 hash.
/// </summary>
[JsonConverter(typeof(MD5HashValueConverter))]
[ValueObject<UInt128>]
public readonly partial struct Md5HashValue
{

    /// <summary>
    /// Parse this from a hexadecimal string.
    /// </summary>
    public static Md5HashValue Parse(string value) => From(UInt128.Parse(value, NumberStyles.HexNumber));


    /// <summary>
    /// Make a MD5 hash from a byte array.
    /// </summary>
    public static Md5HashValue From(byte[] bytes)
    {
        if (bytes.Length != 16)
            throw new ArgumentException("MD5 hash must be 16 bytes long.", nameof(bytes));
        
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return From(MemoryMarshal.Read<UInt128>(bytes));
    }


    /// <inheritdoc />
    public override string ToString() => Value.ToString("x8");

}

/// <summary>
/// JSON converter for <see cref="Md5HashValue"/>.
/// </summary>
internal class MD5HashValueConverter : JsonConverter<Md5HashValue>
{
    public override Md5HashValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();
        return Md5HashValue.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, Md5HashValue value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}