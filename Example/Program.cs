using BinaryBufferLib;

DynamicBuffer db = new();
db.Write((byte)1);
db.WriteLE((short)2);
db.WriteBE(3u);
db.Write(new byte[] { 4, 5 });
db.Write((IReadOnlyList<byte>)new byte[] { 6, 7 });
db.Write(new byte[] { 8, 9 }.AsSpan());
db.Write(Enumerable.Range(10, 2).Select(x => (byte)x));

Console.WriteLine(Convert.ToHexString(db.AsSpan()));

BufferReader br = new(db);
Console.WriteLine(br.ReadByte());
Console.WriteLine(br.ReadInt16LE());
Console.WriteLine(br.ReadUInt32BE());

var ToString = (IEnumerable<byte> x) => string.Join(" ", x.Select(x => x.ToString()));

var array = new byte[2];
br.Read(array);
Console.WriteLine(ToString(array));

List<byte> list = [0, 0];
br.Read(list);
Console.WriteLine(ToString(list));

Span<byte> span = [0, 0];
br.Read(span);
Console.WriteLine(ToString(span.ToArray()));

Console.WriteLine(ToString(br.ReadBytes().Take(2)));

Span<byte> buffer = stackalloc byte[4];
FixedBufferWriter fbw = new(buffer);
fbw.WriteNE(100);

Console.WriteLine(Convert.ToHexString(buffer));

FixedBufferReader fbr = new(fbw);
Console.WriteLine(fbr.ReadUInt32NE());