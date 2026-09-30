using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Format.Print.Api.Cups;

/// <summary>Коды групп и типов значений IPP (RFC 8010).</summary>
internal static class IppTag
{
    public const byte OperationGroup = 0x01;
    public const byte JobGroup = 0x02;
    public const byte End = 0x03;

    public const byte Integer = 0x21;
    public const byte Boolean = 0x22;
    public const byte Enum = 0x23;
    public const byte Text = 0x41;
    public const byte Name = 0x42;
    public const byte Keyword = 0x44;
    public const byte Uri = 0x45;
    public const byte Charset = 0x47;
    public const byte NaturalLanguage = 0x48;
    public const byte MimeMediaType = 0x49;
}

internal static class IppOperation
{
    public const ushort PrintJob = 0x0002;
    public const ushort CancelJob = 0x0008;
    public const ushort GetJobAttributes = 0x0009;
    public const ushort GetPrinterAttributes = 0x000B;
}

/// <summary>Собирает двоичный IPP-запрос.</summary>
internal sealed class IppWriter
{
    private readonly MemoryStream _buffer = new();

    public IppWriter(ushort operationId, int requestId)
    {
        _buffer.WriteByte(2); // версия IPP 2.0
        _buffer.WriteByte(0);
        WriteUInt16(operationId);
        WriteInt32(requestId);
    }

    public IppWriter Group(byte groupTag)
    {
        _buffer.WriteByte(groupTag);
        return this;
    }

    public IppWriter String(byte valueTag, string name, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteHeader(valueTag, name, (ushort)bytes.Length);
        _buffer.Write(bytes);
        return this;
    }
    
    /// <summary>Атрибут с несколькими значениями (1setOf): имя пишется только у первого.</summary>
    public IppWriter Strings(byte valueTag, string name, params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(values[i]);
            WriteHeader(valueTag, i == 0 ? name : "", (ushort)bytes.Length);
            _buffer.Write(bytes);
        }
        return this;
    }

    public IppWriter Integer(string name, int value)
    {
        WriteHeader(IppTag.Integer, name, 4);
        WriteInt32(value);
        return this;
    }

    public byte[] Build()
    {
        _buffer.WriteByte(IppTag.End);
        return _buffer.ToArray();
    }

    private void WriteHeader(byte valueTag, string name, ushort valueLength)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        _buffer.WriteByte(valueTag);
        WriteUInt16((ushort)nameBytes.Length);
        _buffer.Write(nameBytes);
        WriteUInt16(valueLength);
    }

    private void WriteUInt16(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        _buffer.Write(bytes);
    }

    private void WriteInt32(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        _buffer.Write(bytes);
    }
}

internal sealed record IppAttribute(byte Tag, string Name, object Value);

/// <summary>Разобранный IPP-ответ.</summary>
internal sealed class IppResponse
{
    private IppResponse(ushort statusCode, IReadOnlyList<IppAttribute> attributes)
    {
        StatusCode = statusCode;
        Attributes = attributes;
    }
    
    public IEnumerable<object> GetAll(string name) =>
        Attributes.Where(a => a.Name == name).Select(a => a.Value);

    public ushort StatusCode { get; }
    public IReadOnlyList<IppAttribute> Attributes { get; }

    /// <summary>Коды 0x0000–0x00FF - успех (в том числе «успех, но часть опций проигнорирована»).</summary>
    public bool IsSuccess => StatusCode < 0x0100;

    public object? Get(string name) => Attributes.FirstOrDefault(a => a.Name == name)?.Value;

    public static IppResponse Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 9)
            throw new CupsException("Слишком короткий IPP-ответ.");

        var status = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var pos = 8; // пропускаем версию, код и номер запроса
        var attributes = new List<IppAttribute>();
        var lastName = "";

        while (pos < data.Length)
        {
            var tag = data[pos++];

            if (tag == IppTag.End)
                break;

            if (tag < 0x10) // начало новой группы атрибутов
                continue;

            var nameLength = ReadUInt16(data, ref pos);
            // Пустое имя - это ещё одно значение предыдущего атрибута (списки вида 1setOf)
            var name = nameLength == 0 ? lastName : Encoding.ASCII.GetString(data.Slice(pos, nameLength));
            pos += nameLength;

            var valueLength = ReadUInt16(data, ref pos);
            var value = data.Slice(pos, valueLength);
            pos += valueLength;

            attributes.Add(new IppAttribute(tag, name, Decode(tag, value)));
            lastName = name;
        }

        return new IppResponse(status, attributes);
    }

    private static object Decode(byte tag, ReadOnlySpan<byte> value) => tag switch
    {
        IppTag.Integer or IppTag.Enum when value.Length == 4 => BinaryPrimitives.ReadInt32BigEndian(value),
        IppTag.Boolean when value.Length == 1 => value[0] != 0,
        >= IppTag.Text and <= IppTag.MimeMediaType => Encoding.UTF8.GetString(value),
        _ => value.ToArray(),
    };

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int pos)
    {
        var value = BinaryPrimitives.ReadUInt16BigEndian(data[pos..]);
        pos += 2;
        return value;
    }
}

/// <summary>Тело HTTP-запроса: IPP-заголовок, сразу за ним документ.</summary>
internal sealed class IppContent : HttpContent
{
    private readonly byte[] _header;
    private readonly Stream _document;

    public IppContent(byte[] header, Stream document)
    {
        _header = header;
        _document = document;
        Headers.ContentType = new MediaTypeHeaderValue("application/ipp");
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(_header);
        await _document.CopyToAsync(stream);
    }

    protected override bool TryComputeLength(out long length)
    {
        if (_document.CanSeek)
        {
            length = _header.Length + (_document.Length - _document.Position);
            return true;
        }

        length = 0;
        return false;
    }
}

public sealed class CupsException(string message) : Exception(message);