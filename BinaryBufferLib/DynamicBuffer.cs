using BinaryBufferLib.Encodings;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BinaryBufferLib
{
    /// <summary>
    /// A dynamically resizing buffer that can be read and written to with automatic endian conversion using stream-like semantics.
    /// </summary>
    public partial class DynamicBuffer 
        : IBufferWriter<byte>
    {
        internal byte[] _buffer;
        internal int _endOffs;
        internal int _pos; // Always 0+

        Common.GrowBufferDelegate _growBuffer;

        /// <summary>
        /// Create a DynamicBuffer with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the buffer to allocate, or negative for default.</param>
        public DynamicBuffer(int initialCapacity = -1)
        {
            if (initialCapacity < 0)
                initialCapacity = 0x200;

            _buffer = new byte[initialCapacity];
            _endOffs = 0;
            _pos = 0;

            _growBuffer = GrowBuffer;
        }

        /// <summary>
        /// The length in bytes of data in the buffer.
        /// </summary>
        public int Length => _endOffs;

        /// <summary>
        /// The current capacity in bytes of the buffer. It may grow with use.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Capacity
        {
            get => _buffer.Length;
            set
            {
                if (value == _buffer.Length)
                    return;

                if (value < _endOffs)
                    throw new ArgumentOutOfRangeException(
                        nameof(value), 
                        "cannot set Capacity smaller than Length");

                var newBuff = new byte[value];
                SliceTo(_endOffs).CopyTo(newBuff);

                _buffer = newBuff;
            }
        }

        /// <summary>
        /// The current position that subsequent reads and writes will occur at.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public int Position
        {
            get => _pos;
            set => Common.SetPosition(out _pos, value);
        }

        public byte this[int index]
        {
            get
            {
                if (index >= _endOffs)
                    throw new IndexOutOfRangeException();

                return _buffer[index];
            }

            set
            {
                if (index >= _endOffs)
                    throw new IndexOutOfRangeException();

                _buffer[index] = value;
            }
        }

        /// <summary>
        /// Get a span over the underlying buffer. This span is only valid until the DynamicBuffer is modified.
        /// </summary>
        /// <returns></returns>
        public Span<byte> AsSpan()
            => _buffer.AsSpan(0, _endOffs);

        /// <summary>
        /// Get a span over the underlying buffer. This span is only valid until the DynamicBuffer is modified.
        /// </summary>
        /// <param name="start">The initial position in bytes to start the span.</param>
        /// <param name="length">The size in bytes of the span, or negative to go to the end of the buffer.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Span<byte> AsSpan(int start = 0, int length = -1)
        {
            if (start < 0 || start > _endOffs)
                throw new ArgumentOutOfRangeException(nameof(start));

            if (length >= 0)
            {
                if (checked(start + length) > _endOffs)
                    throw new ArgumentOutOfRangeException(nameof(length));
            }
            else
                length = _endOffs - start;

            return _buffer.AsSpan(start, length);
        }

        /// <inheritdoc />
        void IBufferWriter<byte>.Advance(int count)
        {
            if (checked(_pos + count) > _buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            _pos += count;
        }

        /// <inheritdoc />
        Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
        {
            EnsureGetSpanCapacity(sizeHint);

            return _buffer.AsMemory(_pos);
        }

        /// <inheritdoc />
        Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
        {
            EnsureGetSpanCapacity(sizeHint);

            return _buffer.AsSpan(_pos);
        }

        /// <summary>
        /// Get a BufferReader for the underlying buffer at the current Position. The reader is only valid until the DynamicBuffer is modified.
        /// </summary>
        /// <returns></returns>
        public BufferReader GetReader()
            => new(this, Position);

        /// <summary>
        /// Ensure that there is at least a specified amount of space in the buffer.
        /// </summary>
        /// <param name="newSize">The minimum buffer size.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public int EnsureCapacity(int newSize)
        {
            if (newSize < 0)
                throw new ArgumentOutOfRangeException(nameof(newSize));
            else if (newSize > _buffer.Length)
                GrowBuffer(newSize, _endOffs);

            return Capacity;
        }

        /// <summary>
        /// Reallocate the buffer to closely fit the current buffer length if there is significant unused capacity in the buffer.
        /// </summary>
        public void TrimExcess(int maxExcessBytes = 0x1000)
        {
            if (_buffer.Length - _endOffs < maxExcessBytes)
                return;

            Capacity = _endOffs;
        }

        /// <summary>
        /// Clear the buffer, resetting its length to 0.
        /// </summary>
        public void Clear()
        {
            _pos = _endOffs = 0;
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
            => Common.Seek(ref _pos, _endOffs, offset, origin);

        /// <summary>
        /// Try to read a byte from the current position without advancing.
        /// </summary>
        /// <returns>The byte read, or -1 if at the end of the stream.</returns>
        public int Peek()
            => Common.Read(_buffer, ref _pos, _buffer.Length, false);

        /// <summary>
        /// Try to read a byte from the current position.
        /// </summary>
        /// <returns>The byte read, or -1 if at the end of the stream.</returns>
        public int Read()
            => Common.Read(_buffer, ref _pos, _buffer.Length, true);

        internal void EnsureGetSpanCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            int totalSize = checked(_pos + Math.Max(sizeHint, 1));
            if (totalSize > _buffer.Length)
                GrowBuffer(totalSize, _endOffs);
        }

        internal Span<byte> GrowBuffer(int minSize, int endOffs)
        {
            Debug.Assert(minSize > _buffer.Length);

            int newSize = _buffer.Length;
            if (newSize != 0)
            {
                while (newSize < minSize)
                    newSize = checked(newSize * 2);
            }
            else
                newSize = minSize;

            var newBuff = new byte[newSize];
            SliceTo(endOffs).CopyTo(newBuff);

            _buffer = newBuff;

            return _buffer.AsSpan();
        }

        internal Span<byte> SliceTo(int pos)
            => _buffer.AsSpan(0, pos);

        internal IEnumerable<T> Enumerate<T, TEnc>()
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            TEnc encoding = default(TEnc);
            int end = checked(_endOffs - encoding.Size + 1);
            for (int pos = _pos; pos < end; pos += encoding.Size)
                yield return encoding.Read(_buffer.AsSpan(pos));
        }
    }
}