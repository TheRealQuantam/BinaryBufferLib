using BinaryBufferLib.Encodings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

[assembly: InternalsVisibleTo("BinaryBufferLib.Test")]

namespace BinaryBufferLib
{
    internal static class Common
    {
        public delegate Span<byte> GrowBufferDelegate(
            int newSize, int endOffs);

        public static void SetPosition(out int pos, int newPos)
        {
            if (newPos < 0)
                throw new ArgumentOutOfRangeException();

            pos = newPos;
        }

        public static int Advance(
            ref int pos,
            int count)
            => Advance<byte>(ref pos, count);

        public static int Advance<T>(
            ref int pos,
            int count)
            where T : unmanaged
        {
            Debug.Assert(pos >= 0);

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            pos = checked(pos + count * Marshal.SizeOf<T>());

            return pos;
        }

        public static int Seek(
            ref int pos, 
            int endOffs, 
            int offset, 
            SeekOrigin origin)
        {
            Debug.Assert(pos >= 0);
            Debug.Assert(endOffs >= 0);

            int newPos = pos;
            checked
            {
                if (origin == SeekOrigin.Begin)
                    newPos = offset;
                else if (origin == SeekOrigin.Current)
                    newPos += offset;
                else if (origin == SeekOrigin.End)
                    newPos = endOffs + offset;
                else
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (newPos < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            pos = newPos;

            return pos;
        }

        public static int Read(
            ReadOnlySpan<byte> source, 
            ref int pos, 
            int endOffs, 
            bool advance)
        {
            Debug.Assert(pos >= 0);
            Debug.Assert(endOffs >= 0);
            Debug.Assert(endOffs <= source.Length);

            if (pos >= endOffs)
                return -1;

            int value = source[pos];

            if (advance)
                pos++;

            return value;
        }

        /*public static bool ReadBool(
            ReadOnlySpan<byte> source,
            ref int pos,
            int endOffs,
            int trueThresh,
            bool advance)
        {
            int b = Read(source, ref pos, endOffs, advance);
            if (b < 0)
                throw new EndOfStreamException();

            return b >= trueThresh;
        }*/

        public static T Read<T, TEnc>(
            ReadOnlySpan<byte> source,
            ref int pos,
            int endOffs,
            bool advance)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            Debug.Assert(pos >= 0);
            Debug.Assert(endOffs >= 0);
            Debug.Assert(endOffs <= source.Length);

            TEnc encoding = default(TEnc);
            int endPos = checked(pos + encoding.Size);
            if (endPos > endOffs)
                throw new EndOfStreamException();

            T value = encoding.Read(source.Slice(pos));

            if (advance)
                pos = endPos;

            return value;
        }

        public static int Read<T, TEnc>(
            ReadOnlySpan<byte> source,
            ref int pos,
            int endOffs,
            Span<T> target,
            int index,
            int count,
            bool advance)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            TEnc encoding = default(TEnc);
            int realCount = ResolveReadCount(
                source, pos, endOffs, encoding.Size, target.Length, index, count);
            int newPos = pos;
            for (int i = 0; i < realCount; i++)
            {
                target[index + i] = encoding.Read(source.Slice(newPos));
                newPos += encoding.Size;
            }

            if (advance)
                pos = newPos;

            return realCount;
        }

        public static int Read<T, TEnc, TList>(
            ReadOnlySpan<byte> source,
            ref int pos,
            int endOffs,
            TList target,
            int index,
            int count,
            bool advance)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            where TList : IList<T>
        {
            TEnc encoding = default(TEnc);
            int realCount = ResolveReadCount(
                source, 
                pos, 
                endOffs, 
                encoding.Size, 
                target.Count, 
                index, count);
            int newPos = pos;
            for (int i = 0; i < realCount; i++)
            {
                target[index++] = encoding.Read(source.Slice(newPos));
                newPos += encoding.Size;
            }

            if (advance)
                pos = newPos;

            return realCount;
        }

        public static void Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            T value)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            TEnc encoding = default(TEnc);
            int newPos = EnsureSpace(
                ref target, pos, endOffs, GrowBuffer, encoding.Size);

            encoding.Write(target.Slice(pos), value);

            endOffs = Math.Max(endOffs, newPos);
            pos = newPos;
        }

        public static void Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            int endOffs,
            T value)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            => Write<T, TEnc>(
                target, 
                ref pos, 
                ref endOffs, 
                DummyGrowBuffer, 
                value);

        public static void Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            T value,
            int count)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            if (count == 0)
                return;

            TEnc encoding = default(TEnc);
            EnsureSpace(
                ref target, 
                pos, 
                endOffs, 
                GrowBuffer, 
                checked(count * encoding.Size));

            int newPos = pos;
            for (int i = 0; i < count; i++)
            {
                encoding.Write(target.Slice(newPos), value);
                newPos += encoding.Size;
            }

            endOffs = Math.Max(endOffs, newPos);
            pos = newPos;
        }

        public static void Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            int endOffs,
            T value,
            int count)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            => Write<T, TEnc>(
                target, 
                ref pos, 
                ref endOffs, 
                DummyGrowBuffer, 
                value, 
                count);

        public static int Write<T, TEnc, TEnum>(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            TEnum values)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            where TEnum : IEnumerable<T>
        {
            TEnc encoding = default(TEnc);
            int newPos = pos, count = 0;
            /*if (values is T[] array)
            {
                return Write<T, TEnc>(
                    target,
                    ref newPos,
                    ref endOffs,
                    GrowBuffer,
                    array.AsSpan(),
                    0,
                    array.Length,
                    advance);
            }
            else if (values is IReadOnlyList<T> list)
            {
                return Write<T, TEnc>(
                    target,
                    ref newPos,
                    ref endOffs,
                    GrowBuffer,
                    list,
                    0,
                    list.Count,
                    advance);
            }
            else*/ if (values is IReadOnlyCollection<T> coll)
            {
                count = coll.Count;
                if (count == 0)
                    return 0;

                EnsureSpace(
                    ref target,
                    newPos,
                    endOffs,
                    GrowBuffer,
                    checked(count * encoding.Size));

                foreach (var value in values)
                {
                    encoding.Write(target.Slice(newPos), value);

                    newPos += encoding.Size;
                }

                endOffs = Math.Max(endOffs, newPos);
            }
            else
            {
                int newEndOffs = endOffs, endPos = 0; // Force EnsureSpace
                foreach (var value in values)
                {
                    if (newPos >= endPos)
                    {
                        if (newPos != pos)
                            // Don't move the end offset ahead at first as that will increase copy size unnecessarily
                            newEndOffs = Math.Max(newEndOffs, newPos);

                        EnsureSpace(
                            ref target,
                            newPos,
                            newEndOffs,
                            GrowBuffer,
                            encoding.Size);
                        endPos = target.Length - encoding.Size + 1;
                    }

                    encoding.Write(target.Slice(newPos), value);

                    newPos += encoding.Size;
                }

                // Only move the end offset if data was written
                if (newPos == pos)
                    return 0;

                count = (newPos - pos) / encoding.Size;
                endOffs = newEndOffs = Math.Max(newEndOffs, newPos);
            }

            pos = newPos;

            return count;
        }

        public static int Write<T, TEnc, TEnum>(
            Span<byte> target,
            ref int pos,
            int endOffs,
            TEnum values)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            where TEnum : IEnumerable<T>
            => Write<T, TEnc, TEnum>(
                target, 
                ref pos, 
                ref endOffs, 
                DummyGrowBuffer, 
                values);

        public static int Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            IReadOnlyList<T> values,
            int index,
            int count)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            TEnc encoding = default(TEnc);
            int realCount = ResolveWriteCount(
                ref target, 
                pos, 
                endOffs, 
                GrowBuffer, 
                encoding.Size, 
                values.Count, 
                index, 
                count);
            if (realCount == 0)
                return 0;

            int newPos = pos;
            for (int i = 0; i < realCount; i++)
            {
                encoding.Write(target.Slice(newPos), values[i + index]);
                newPos += encoding.Size;
            }

            endOffs = Math.Max(endOffs, newPos);
            pos = newPos;

            return realCount;
        }

        public static int Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            int endOffs,
            IReadOnlyList<T> values,
            int index,
            int count)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            => Write<T, TEnc>(
                target, 
                ref pos, 
                ref endOffs, 
                DummyGrowBuffer, 
                values, 
                index, 
                count);

        public static int Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            ReadOnlySpan<T> values,
            int index,
            int count)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
        {
            TEnc encoding = default(TEnc);
            int realCount = ResolveWriteCount(
                ref target, 
                pos, 
                endOffs, 
                GrowBuffer, 
                encoding.Size, 
                values.Length, 
                index, 
                count);
            if (realCount == 0)
                return 0;

            int newPos = pos;
            for (int i = 0; i < realCount; i++)
            {
                encoding.Write(target.Slice(newPos), values[index + i]);
                newPos += encoding.Size;
            }

            endOffs = Math.Max(endOffs, newPos);
            pos = newPos;

            return realCount;
        }

        public static int Write<T, TEnc>(
            Span<byte> target,
            ref int pos,
            int endOffs,
            ReadOnlySpan<T> values,
            int index,
            int count)
            where T : unmanaged
            where TEnc : struct, IBinaryEncoding<T>
            => Write<T, TEnc>(
                target, 
                ref pos, 
                ref endOffs, 
                DummyGrowBuffer, 
                values, 
                index, 
                count);

        public static int Write(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            string str,
            Encoding encoding,
            int maxBytes,
            bool fill)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            int newPos = EnsureSpace(
                ref target, pos, endOffs, GrowBuffer, maxBytes);
            int strSize = encoding.GetBytes(
                (ReadOnlySpan<char>)str, target.Slice(pos, maxBytes));

            if (fill && strSize < maxBytes)
            {
                // Pad to the specified size
                target.Slice(pos + strSize, maxBytes - strSize)
                    .Fill((byte)0);
            }
            else
                newPos = pos + strSize;

            int totalSize = newPos - pos;

            endOffs = Math.Max(endOffs, newPos);
            pos = newPos;

            return totalSize;
        }

        public static int Write(
            Span<byte> target,
            ref int pos,
            int endOffs,
            string str,
            Encoding encoding,
            int maxBytes,
            bool fill)
            => Write(
                target, 
                ref pos, 
                ref endOffs,
                DummyGrowBuffer,
                str, 
                encoding, 
                maxBytes, 
                fill);

        public static int Write(
            Span<byte> target,
            ref int pos,
            ref int endOffs,
            GrowBufferDelegate GrowBuffer,
            string str,
            Encoding encoding)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            int strSize = encoding.GetByteCount(str);
            if (strSize > 0)
            {
                int newPos = EnsureSpace(
                    ref target, pos, endOffs, GrowBuffer, strSize);
                encoding.GetBytes(
                    (ReadOnlySpan<char>)str, target.Slice(pos));

                endOffs = Math.Max(endOffs, newPos);
                pos = newPos;
            }

            return strSize;
        }

        public static int Write(
            Span<byte> target,
            ref int pos,
            int endOffs,
            string str,
            Encoding encoding)
            => Write(target, 
                ref pos, 
                ref endOffs, 
                DummyGrowBuffer, 
                str, 
                encoding);

        internal static int ResolveReadCount(
            ReadOnlySpan<byte> buffer,
            int pos,
            int endOffs,
            int elementSize,
            int bufferLength,
            int index,
            int count)
        {
            Debug.Assert(pos >= 0);
            Debug.Assert(endOffs >= 0);
            Debug.Assert(endOffs <= buffer.Length);
            Debug.Assert(elementSize > 0);
            Debug.Assert(bufferLength >= 0);

            if (count < 0)
                count = Math.Max(checked(bufferLength - index), 0);
            else if (checked(index + count) > bufferLength)
                throw new IndexOutOfRangeException();

            int newPos = checked(pos + count * elementSize);
            if (newPos > endOffs)
                throw new EndOfStreamException();

            return count;
        }

        internal static int ResolveWriteCount(
            ref Span<byte> buffer,
            int pos, 
            int endOffs, 
            GrowBufferDelegate GrowBuffer,
            int elementSize, 
            int bufferLength, 
            int index = 0, 
            int count = -1)
        {
            Debug.Assert(elementSize > 0);
            Debug.Assert(bufferLength >= 0);
            Debug.Assert(index >= 0);

            if (count < 0)
                count = Math.Max(checked(bufferLength - index), 0);
            else if (checked(index + count) > bufferLength)
                throw new IndexOutOfRangeException();

            if (count == 0)
                return 0;

            EnsureSpace(
                ref buffer, 
                pos, 
                endOffs, 
                GrowBuffer, 
                checked(count * elementSize));

            return count;
        }

        internal static int EnsureSpace(
            ref Span<byte> buffer,
            int pos,
            int endOffs,
            GrowBufferDelegate GrowBuffer,
            int size)
        {
            Debug.Assert(pos >= 0);
            Debug.Assert(size >= 0);

            int newSize = checked(pos + size);
            if (newSize > buffer.Length)
                buffer = GrowBuffer(newSize, endOffs);

            return newSize;
        }

        internal static Span<byte> DummyGrowBuffer(int minSize, int endOffs)
        {
            Debug.Assert(minSize > endOffs);

            throw new IOException("write exceeds buffer capacity");
        }
    }
}
