using BinaryBufferLib.Encodings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace BinaryBufferLib
{
    /// <summary>
    /// A lightweight utility that can be used to read bytes and other types with automatic endian conversion from a ReadOnlyMemory&lt;byte&gt; using stream-like semantics.
    /// </summary>
    public partial struct BufferReader : IReadOnlyList<byte>
    {
        internal readonly ReadOnlyMemory<byte> _buffer;
        internal int _pos; // Always 0+

        /// <summary>
        /// Create a BufferReader on the specified ReadOnlyMemory&lt;byte&gt;.
        /// </summary>
        /// <param name="buffer">The buffer to use.</param>
        /// <param name="position">The initial position for reads.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public BufferReader(ReadOnlyMemory<byte> buffer, int position = 0)
        {
            if (position < 0)
                throw new ArgumentOutOfRangeException(nameof(position));

            _buffer = buffer;
            _pos = position;
        }

        /// <summary>
        /// Create a BufferReader from a DynamicBuffer. The BufferReader is only valid until the DynamicBuffer is modified.
        /// </summary>
        /// <param name="buffer">The buffer to use.</param>
        /// <param name="position">The initial Position for reads, or negative to use the DynamicBuffer's current Position.</param>
        public BufferReader(DynamicBuffer buffer, int position = 0)
            : this(buffer._buffer.AsMemory(), 
                  position >= 0 ? position : buffer._pos)
        { }

        /// <summary>
        /// The underlying buffer.
        /// </summary>
        public ReadOnlyMemory<byte> Memory => _buffer;

        /// <inheritdoc />
        int IReadOnlyCollection<byte>.Count => _buffer.Length;

        /// <summary>
        /// The length in bytes of data in the buffer.
        /// </summary>
        public int Length => _buffer.Length;

        /// <summary>
        /// The current position that subsequent reads and writes will occur at.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Position
        {
            get => _pos;
            set => Common.SetPosition(out _pos, value);
        }

        /// <inheritdoc />
        public byte this[int index] => _buffer.Span[index];

        /// <summary>
        /// Get a span over the underlying buffer.
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> AsSpan()
            => _buffer.Span;

        /// <summary>
        /// Get a span over the underlying buffer.
        /// </summary>
        /// <param name="start">The initial position in bytes to start the span.</param>
        /// <param name="length">The size in bytes of the span, or negative to go to the end of the buffer.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ReadOnlySpan<byte> AsSpan(int start = 0, int length = -1)
            => _buffer.Span.Slice(
                start, length > 0 ? length : _buffer.Length);

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

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<byte>)this).GetEnumerator();

        /// <inheritdoc />
        IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
        {
            for (int i = 0; i < _buffer.Length; i++)
                yield return _buffer.Span[i];
        }

        /// <summary>
        /// Gets an enumerator over the underlying buffer.
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte>.Enumerator GetEnumerator()
            => _buffer.Span.GetEnumerator();

        /// <summary>
        /// Try to read a byte from the current position.
        /// </summary>
        /// <param name="advance">Whether to advance the current position.</param>
        /// <returns>The byte read, or -1 if at the end of the stream.</returns>
        public int Read(bool advance = true)
            => Common.Read(_buffer.Span, ref _pos, _buffer.Length, advance);

        internal IEnumerable<T> Enumerate<T, TEnc>()
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            TEnc encoding = default(TEnc);
            int end = checked(_buffer.Length - encoding.Size + 1);
            for (int pos = _pos; pos < end; pos += encoding.Size)
                yield return encoding.Read(_buffer.Span.Slice(pos));
        }
    }
}