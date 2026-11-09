using BinaryBufferLib;

DynamicBuffer db = new();
db.Write((byte)1);
db.WriteLE((short)2);
db.WriteBE(3u);
db.WriteLE(-1);
db.Write(new byte[] { 4, 5 });
db.Write((IReadOnlyList<byte>)new byte[] { 6, 7 });
db.Write(new byte[] { 8, 9 }.AsSpan());
db.Write(Enumerable.Range(10, 2).Select(x => (byte)x));

Console.WriteLine(Convert.ToHexString(db.AsSpan()));

BufferReader br = new(db);
Console.WriteLine(br.ReadByte());
Console.WriteLine(br.ReadInt16LE());
Console.WriteLine(br.ReadUInt32BE());

br.Advance<int>(1);

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

// NOTE: Reading an enumerable does not update Position
Console.WriteLine(br.Position);
Console.WriteLine(ToString(br.PeekBytes().Take(2)));
Console.WriteLine(br.Position);
Console.WriteLine(br.Advance<byte>(2));

Span<byte> buffer = stackalloc byte[4];
FixedBuffer fb = new(buffer);
fb.WriteNE(100);

Console.WriteLine(Convert.ToHexString(buffer));

FixedBufferReader fbr = new(fb);
Console.WriteLine(fbr.ReadInt32NE());