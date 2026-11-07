using System;
using System.IO;

namespace BinaryBufferLib
{
    /// <summary>
    /// A lightweight utility type that can be used to write bytes and other types with automatic endian conversion to a fixed-size Span-based buffer using stream-like semantics. Simpler and faster than DynamicBuffer, offering no support for resizable buffers.
    /// </summary>
    public ref struct FixedBufferWriter
    {
        internal Span<byte> _buffer;
        internal int _pos; // Always 0+

        /// <summary>
        /// The Span used to create the buffer.
        /// </summary>
        public Span<byte> Span => _buffer;

        /// <summary>
        /// Create a FixedBufferWriter over a Span<byte></byte>.
        /// </summary>
        /// <param name="buffer"></param>
        public FixedBufferWriter(Span<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
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
        public byte this[int index]
        {
            get => _buffer[index];
            set => _buffer[index] = value;
        }

        /// <summary>
        /// Get a Span over the underlying buffer.
        /// </summary>
        /// <returns></returns>
        public Span<byte> AsSpan()
            => _buffer;

        /// <summary>
        /// Get a Span from the start of the buffer to the current position.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException">The current position is past the end of the buffer.</exception>
        public Span<byte> AsSpanToPosition()
        {
            if (_pos > _buffer.Length)
                throw new EndOfStreamException();

            return _buffer.Slice(_pos);
        }

        /// <summary>
        /// Set the current position for subsequent reads and writes.
        /// </summary>
        /// <param name="offset">The offset from the specified origin.</param>
        /// <param name="origin">The origin for the seek.</param>
        /// <returns>The new absolute position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Seek(int offset, SeekOrigin origin = SeekOrigin.Begin)
            => Common.Seek(ref _pos, _buffer.Length, offset, origin);
    }
}