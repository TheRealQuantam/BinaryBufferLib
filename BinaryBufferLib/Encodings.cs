using System;

namespace BinaryBufferLib.Encodings
{
    internal interface IBinaryEncoding<T>
        where T : struct
    {
        int Size { get; }

        T Read(ReadOnlySpan<byte> buffer);
        void Write(Span<byte> buffer, T value);
    }

    internal struct ByteEncoding : IBinaryEncoding<byte>
    {
        public int Size => sizeof(byte);

        public readonly byte Read(ReadOnlySpan<byte> buffer)
            => buffer[0];
        public readonly void Write(Span<byte> buffer, byte value)
            => buffer[0] = value;
    }

    internal struct SByteEncoding : IBinaryEncoding<sbyte>
    {
        public int Size => sizeof(sbyte);

        public readonly sbyte Read(ReadOnlySpan<byte> buffer)
            => (sbyte)buffer[0];
        public readonly void Write(Span<byte> buffer, sbyte value)
            => buffer[0] = (byte)value;
    }
}
