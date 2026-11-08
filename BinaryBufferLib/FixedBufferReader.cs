using System;
using System.IO;

namespace BinaryBufferLib
{
    /// <summary>
    /// A lightweight utility type that can be used to read bytes and other types with automatic endian conversion from a fixed-size Span-based buffer using stream-like semantics. Simpler and faster than DynamicBuffer, offering no support for resizable buffers.
    /// </summary>
    public ref struct FixedBufferReader
    {
        internal ReadOnlySpan<byte> _buffer;
        internal int _pos; // Always 0+

        /// <summary>
        /// The Span used to create the buffer.
        /// </summary>
        public ReadOnlySpan<byte> Span => _buffer;

        /// <summary>
        /// Create a FixedBufferReader over a Span<byte></byte>.
        /// </summary>
        /// <param name="buffer"></param>
        public FixedBufferReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        /// <summary>
        /// Create a FixedBufferReader from a FixedBuffer. The reader will use the same Span as the writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="position">The initial Position for reads, or negative to use the FixedBuffer's current Position.</param>
        public FixedBufferReader(FixedBuffer writer, int position = 0)
        {
            _buffer = writer._buffer;
            _pos = position >= 0 ? position : writer._pos;
        }

        /// <summary>
        /// The current length in bytes of the buffer.
        /// </summary>
        public int Length => _buffer.Length;

        /// <summary>
        /// The current position that subsequent reads or writes will occur at.
        /// </summary>
        public int Position
        {
            get => _pos;
            set => Common.SetPosition(out _pos, value);
        }

        /// <summary>
        /// Access an arbitrary byte in the buffer without using/updating the current position.
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <returns></returns>
        public byte this[int index] => _buffer[index];

        /// <summary>
        /// Get a Span over the underlying buffer.
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> AsSpan()
            => _buffer;

        /// <summary>
        /// Get a Span from the start of the buffer to the current position.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException">The current position is past the end of the buffer.</exception>
        public ReadOnlySpan<byte> AsSpanToPosition()
        {
            if (_pos > _buffer.Length)
                throw new EndOfStreamException();

            return _buffer.Slice(_pos);
        }

        /// <summary>
        /// Advance the current position by numBytes bytes.
        /// </summary>
        /// <param name="numBytes">The number of bytes to advance.</param>
        /// <returns>The new position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Advance(int numBytes)
            => Common.Advance(ref _pos, numBytes);

        /// <summary>
        /// Advance the current position by count Ts.
        /// </summary>
        /// <typeparam name="T">The type to advance a multiple of.</typeparam>
        /// <param name="count">The count to advance.</param>
        /// <returns>The new position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Advance<T>(int count = 1)
            where T : unmanaged
            => Common.Advance<T>(ref _pos, count);

        /// <summary>
        /// Set the current position for subsequent reads and writes.
        /// </summary>
        /// <param name="offset">The offset from the specified origin.</param>
        /// <param name="origin">The origin for the seek.</param>
        /// <returns>The new absolute position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Seek(int offset, SeekOrigin origin = SeekOrigin.Begin)
            => Common.Seek(ref _pos, _buffer.Length, offset, origin);

        /// <summary>
        /// Try to read a byte from the current position without advancing.
        /// </summary>
        /// <returns>The byte read, or -1 if at the end of the stream.</returns>
        public int Read(bool advance = true)
            => Common.Read(_buffer, ref _pos, _buffer.Length, false);

        /// <summary>
        /// Try to read a byte from the current position.
        /// </summary>
        /// <returns>The byte read, or -1 if at the end of the stream.</returns>
        public int Read()
            => Common.Read(_buffer, ref _pos, _buffer.Length, true);
    }
}