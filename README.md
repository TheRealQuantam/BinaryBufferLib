# BinaryBufferLib

A library of lightweight objects for reading and writing binary data that prioritizes convenience. Access is stream-like, allowing for efficient serialization. Numerous value-added features such as automatic conversion between endians and reading and writing enumerables exist.

## Overview

Four classes designed for different use cases, forming two pairs:
- DynamicBuffer: A class with a self-resizing buffer that bytes and binary types can be written to, intended for writing out entire files or variable-sized structures.
- BufferReader: A struct that wraps a Memory and allows bytes and binary types to be read.
- FixedBufferWriter and FixedBufferReader: Smaller, simpler ref structs wrapping (ReadOnly)Span<byte>s that are closely analogous to DynamicBuffer/BufferReader, but lack some features that cannot be supported with ref structs. Intended for reading and writing individual, fixed-size structures.

Key features:
- Stream-like semantics with Length, Position, and Seek
- Additional list-like features such as Capacity and EnsurceCapacity (DynamicBuffer only), this[]
- DynamicBuffer implements the IBufferWriter&lt;byte&gt; interface for writing custom types
- Big, little, and native endian versions of all read/write functions exist
- Support for built-in integer and floating-point types
- Support for arrays through type[], (ReadOnly)Span, I(ReadOnly)List, IEnumerable (reading arrays as IEnumerables is only supported for BufferReader)

## Usage

```csharp
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
```