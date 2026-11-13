using BinaryBufferLib.Encodings;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace BinaryBufferLib.Test;

class BufferWrapper(byte[] buffer)
{
    public byte[] Buffer { get; private set; } = buffer;

    public Span<byte> GrowBuffer(int minSize, int endOffs)
    {
        var newBuff = new byte[minSize];
        Buffer.AsSpan(0, endOffs).CopyTo(newBuff);

        Buffer = newBuff;

        return newBuff;
    }
}

public class TestCommon
{
    [InlineData("valid bytes", 2, false, 8, 10)]
    [InlineData("valid ints", 2, true, 8, 34)]
    [InlineData("negative offset", -1, false, 0, 0, typeof(TraceAssertException))]
    [InlineData("negative size", 2, false, -1, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData("add overflow", 2, false, int.MaxValue, 0, typeof(OverflowException))]
    [InlineData("multiply overflow", 2, true, int.MaxValue, 0, typeof(OverflowException))]
    [Theory]
    public void TestAdvance(
        string testName,
        int pos,
        bool isInt,
        int count,
        int result,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        int newPos = pos;
        Func<int> Advance = isInt 
            ? () => Common.Advance<int>(ref newPos, count) 
            : () => Common.Advance(ref newPos, count);

        if (exceptionType != null)
            Assert.Throws(exceptionType, () => Advance());
        else
        {
            int res = Advance();
            Assert.Equal(result, res);
            Assert.Equal(result, newPos);
        }
    }

    [InlineData("negative offset from start", -1, SeekOrigin.Begin, 64, typeof(ArgumentOutOfRangeException))]
    [InlineData("start from start", 0, SeekOrigin.Begin, 0)]
    [InlineData("end from start", 128, SeekOrigin.Begin, 128)]
    [InlineData("negative offset from current", -65, SeekOrigin.Current, 64, typeof(ArgumentOutOfRangeException))]
    [InlineData("start from current", -64, SeekOrigin.Current, 0)]
    [InlineData("current from current", 0, SeekOrigin.Current, 64)]
    [InlineData("end from current", 64, SeekOrigin.Current, 128)]
    [InlineData("negative offset from end", -129, SeekOrigin.End, 64, typeof(ArgumentOutOfRangeException))]
    [InlineData("start from end", -128, SeekOrigin.End, 0)]
    [InlineData("end from end", 0, SeekOrigin.End, 128)]
    [Theory]
    public void TestSeek(
        string testName,
        int offset,
        SeekOrigin origin,
        int result,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        int pos = 64;
        try
        {
            int newPos = Common.Seek(ref pos, 128, offset, origin);

            Assert.Null(exceptionType);
            Assert.Equal(result, newPos);
            Assert.Equal(result, pos);
        }
        catch (XunitException)
        { throw; }
        catch (Exception e)
        {
            Assert.NotNull(exceptionType);
            Assert.IsType(exceptionType, e);
            Assert.Equal(64, pos);
        }
    }

    [InlineData("negative pos", -1, 0, 0, typeof(TraceAssertException))]
    [InlineData("negative endOffs", 0, -1, 0, typeof(TraceAssertException))]
    [InlineData("endOffs past end", 0, 0x101, 0, typeof(TraceAssertException))]
    [InlineData("pos past end", 0x100, 0x100, -1)]
    [InlineData("success", 0xff, 0x100, 0xff)]
    [Theory]
    public void TestReadByte(
        string testName,
        int pos,
        int endOffs,
        int _value,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        var buff = CreateStandardBuffer();
        foreach (bool advance in TestUtils.EnumBool())
        {
            int newPos = pos;
            try
            {
                int tgtPos = pos + (advance  && _value >= 0 ? 1 : 0);
                int value = Common.Read(buff, ref newPos, endOffs, advance);

                Assert.Null(exceptionType);
                Assert.Equal(_value, value);
                Assert.Equal(tgtPos, newPos);
            }
            catch (XunitException) 
            { throw; }
            catch (Exception e)
            {
                Assert.NotNull(exceptionType);
                Assert.IsType(exceptionType, e);
                Assert.Equal(pos, newPos);
            }
        }
    }

    [InlineData("negative pos", -1, 0, 0, 1, null, typeof(TraceAssertException))]
    [InlineData("negative endOffs", 0, -1, 0, 1, null, typeof(TraceAssertException))]
    [InlineData("endOffs past end", 0, 0x101, 0, 1, null, typeof(TraceAssertException))]
    [InlineData("pos past end", 0xf9, 0x100, 0, 1, null, typeof(EndOfStreamException))]
    [InlineData("single", 0xf8, 0x100, 0, 1, new UInt64[] { 0xfffefdfcfbfaf9f8 })]
    [InlineData("multi", 0xe0, 0x100, 0xfc, 4, new UInt64[] { 0xe7e6e5e4e3e2e1e0, 0xefeeedecebeae9e8, 0xf7f6f5f4f3f2f1f0, 0xfffefdfcfbfaf9f8 })]
    [Theory]
    public void TestRead(
        string testName,
        int pos,
        int endOffs,
        int index,
        int count,
        UInt64[]? _values,
        Type? exceptionType = null)
    {
        // Note: because the Read function is generic, so long as the encodings are working properly we don't actually need to test the function for all data types, as the logic is identical. Test on the largest type as that may reveal issues that bytes would not.
        if (ShouldSkipAssert(exceptionType))
            return;

        var buff = CreateStandardBuffer();
        foreach (bool isMulti in TestUtils.EnumBool(
            index == 0 && count == 1 ? null : true))
        {
            foreach (bool isSpan in TestUtils.EnumBool())
            {
                int newPos = pos;
                foreach (bool advance in TestUtils.EnumBool())
                {
                    int tgtCount = 0;
                    if (exceptionType == null)
                    {
                        Assert.NotNull(_values);

                        tgtCount = _values.Length;
                    }

                    try
                    {
                        int tgtPos = pos + (advance ? sizeof(UInt64) * tgtCount : 0);

                        if (isMulti)
                        {
                            var values = new UInt64[0x100];
                            int numRead;
                            if (isSpan)
                                numRead = Common.Read<UInt64, UInt64LEEncoding>(buff, ref newPos, endOffs, values.AsSpan(), index, count, advance);
                            else
                                numRead = Common.Read<UInt64, UInt64LEEncoding, UInt64[]>(buff, ref newPos, endOffs, values, index, count, advance);

                            Assert.Equal(tgtCount, numRead);
                            Assert.Equal(new UInt64[index].AsSpan(), values.AsSpan(0, index));
                            Assert.Equal(_values.AsSpan(), values.AsSpan(index, tgtCount));
                        }
                        else
                        {
                            UInt64 value = Common.Read<UInt64, UInt64LEEncoding>(buff, ref newPos, endOffs, advance);

                            Assert.Equal(_values![0], value);
                        }

                        Assert.Equal(tgtPos, newPos);
                    }
                    catch (XunitException)
                    { throw; }
                    catch (Exception e)
                    {
                        Assert.NotNull(exceptionType);
                        Assert.IsType(exceptionType, e);
                        Assert.Equal(pos, newPos);
                    }
                }
            }
        }
    }

    [InlineData("empty", 0x200, 0x100, null, 0, 0, new byte[0])]
    [InlineData("to end", 0xf8, 0x100, null, 0x0807060504030201, 1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData("to end multi", 0xe8, 0x100, null, 0x0807060504030201, 3, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData("past end fixed", 0x100, 0x100, false, 0x0807060504030201, 1, null, typeof(IOException))]
    [InlineData("past end grow", 0x100, 0x100, true, 0x0807060504030201, 1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData("past end grow multi", 0x100, 0x100, true, 0x0807060504030201, 3, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [Theory]
    public void TestWrite(
        string testName,
        int pos,
        int endOffs,
        bool? canGrow,
        UInt64 value,
        int count,
        byte[]? _bytes,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        const int valSize = sizeof(UInt64);
        int checkPos = Math.Min(pos, endOffs);
        int tgtSize = 0;
        if (exceptionType == null)
        {
            if (count >= 0)
                tgtSize = count * valSize;

            Assert.NotNull(_bytes);
            Assert.Equal(Math.Min(tgtSize, valSize), _bytes.Length);
        }

        Span<byte> checkBuff = stackalloc byte[tgtSize];
        for (int i = 0; i < tgtSize / valSize; i++)
            _bytes.CopyTo(checkBuff.Slice(i * valSize));

        var cleanBuff = CreateStandardBuffer();
        int tgtPos = pos + tgtSize;
        int tgtEndOffs = tgtSize != 0
            ? Math.Max(pos + tgtSize, endOffs)
            : endOffs;
        foreach (bool isGrowable in TestUtils.EnumBool(canGrow))
        {
            foreach (int type in Enumerable.Range(0, 2))
            {
                BufferWrapper buffWrap = new(cleanBuff.ToArray());
                int newPos = pos,
                    newEndOffs = endOffs;

                try
                {
                    if (isGrowable)
                    {
                        switch (type)
                        {
                            case 0:
                                if (count != 1)
                                    continue;

                                Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    ref newEndOffs,
                                    buffWrap.GrowBuffer,
                                    value);

                                break;

                            default:
                                Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    ref newEndOffs,
                                    buffWrap.GrowBuffer,
                                    value,
                                    count);

                                break;
                        }
                    }
                    else
                    {
                        switch (type)
                        {
                            case 0:
                                if (count != 1)
                                    continue;

                                Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    newEndOffs,
                                    value);

                                break;

                            default:
                                Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    newEndOffs,
                                    value,
                                    count);

                                break;
                        }
                    }

                    Assert.Equal(tgtSize, count * valSize);
                    Assert.True(buffWrap.Buffer.Length >= tgtEndOffs);
                    Assert.Equal(tgtPos, newPos);
                    Assert.Equal(tgtEndOffs, newEndOffs);

                    if (tgtSize != 0)
                    {
                        Assert.Equal(
                            buffWrap.Buffer.AsSpan(pos, tgtSize),
                            checkBuff);
                    }
                }
                catch (XunitException)
                { throw; }
                catch (Exception e)
                {
                    Assert.NotNull(exceptionType);
                    Assert.IsType(exceptionType, e);
                    Assert.Equal(pos, newPos);
                    Assert.Equal(endOffs, newEndOffs);
                }

                Assert.Equal(cleanBuff.AsSpan(0, checkPos),
                    buffWrap.Buffer.AsSpan(0, checkPos));
            }
        }
    }

    [InlineData("empty", 0x200, 0x100, null, new UInt64[0], 0, 0, new byte[0])]
    [InlineData("to end", 0xf0, 0x100, null, new UInt64[] { 0x0807060504030201, 0x100f0e0d0c0b0a09, 0, 0 }, 0, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]
    [InlineData("to end skewed", 0xf0, 0x100, null, new UInt64[] { 0, 0, 0x0807060504030201, 0x100f0e0d0c0b0a09, 0, 0 }, 2, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]
    [InlineData("past end fixed", 0x100, 0x100, false, new UInt64[] { 0x0807060504030201, 0x100f0e0d0c0b0a09, 0, 0 }, 0, 2, null, typeof(IOException))]
    [InlineData("past end skewed fixed", 0x100, 0x100, false, new UInt64[] { 0, 0, 0x0807060504030201, 0x100f0e0d0c0b0a09, 0, 0 }, 2, 2, null, typeof(IOException))]
    [InlineData("past end grow", 0x100, 0x100, true, new UInt64[] { 0x0807060504030201, 0x100f0e0d0c0b0a09, 0, 0 }, 0, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]
    [InlineData("past end skewed grow", 0x100, 0x100, true, new UInt64[] { 0, 0, 0x0807060504030201, 0x100f0e0d0c0b0a09, 0, 0 }, 2, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]
    [Theory]
    public void TestWriteMultiple(
        string testName,
        int pos,
        int endOffs,
        bool? canGrow,
        UInt64[] values,
        int index,
        int count,
        byte[]? _bytes,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        const int valSize = sizeof(UInt64);
        int checkPos = Math.Min(pos, endOffs);
        int tgtSize = 0;
        if (exceptionType == null)
        {
            Assert.NotNull(_bytes);
            if (count >= 0)
                Assert.Equal(count * valSize, _bytes.Length);

            tgtSize = _bytes.Length;
        }

        var cleanBuff = CreateStandardBuffer();
        int tgtPos = pos + tgtSize;
        int tgtEndOffs = tgtSize != 0
            ? Math.Max(pos + tgtSize, endOffs)
            : endOffs;
        foreach (bool isGrowable in TestUtils.EnumBool(canGrow))
        {
            foreach (int type in Enumerable.Range(0, 4))
            {
                BufferWrapper buffWrap = new(cleanBuff.ToArray());
                int newPos = pos,
                    newEndOffs = endOffs,
                    numWritten = 1;

                try
                {
                    if (isGrowable)
                    {
                        switch (type)
                        {
                            case 0:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding, IEnumerable<UInt64>>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    ref newEndOffs,
                                    buffWrap.GrowBuffer,
                                    values.Skip(index).Take(count));

                                break;

                            case 1:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding, IEnumerable<UInt64>>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    ref newEndOffs,
                                    buffWrap.GrowBuffer,
                                    values.AsSpan(index, count).ToArray());

                                break;

                            case 2:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    ref newEndOffs,
                                    buffWrap.GrowBuffer,
                                    values.AsSpan(),
                                    index,
                                    count);

                                break;

                            default:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    ref newEndOffs,
                                    buffWrap.GrowBuffer,
                                    (IReadOnlyList<UInt64>)values,
                                    index,
                                    count);

                                break;
                        }
                    }
                    else
                    {
                        switch (type)
                        {
                            case 0:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding, IEnumerable<UInt64>>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    newEndOffs,
                                    values.Skip(index).Take(count));

                                break;

                            case 1:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding, IEnumerable<UInt64>>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    newEndOffs,
                                    values.AsSpan(index, count).ToArray());

                                break;

                            case 2:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    newEndOffs,
                                    values.AsSpan(),
                                    index,
                                    count);

                                break;

                            default:
                                numWritten = Common.Write<UInt64, UInt64LEEncoding>(
                                    buffWrap.Buffer,
                                    ref newPos,
                                    newEndOffs,
                                    (IReadOnlyList<UInt64>)values,
                                    index,
                                    count);

                                break;
                        }
                    }

                    Assert.Equal(tgtSize, numWritten * valSize);
                    Assert.True(buffWrap.Buffer.Length >= tgtEndOffs);
                    Assert.Equal(tgtPos, newPos);
                    Assert.Equal(tgtEndOffs, newEndOffs);

                    if (tgtSize != 0)
                    {
                        Assert.Equal(
                            buffWrap.Buffer.AsSpan(pos, tgtSize),
                            _bytes.AsSpan());
                    }
                }
                catch (XunitException)
                { throw; }
                catch (Exception e)
                {
                    Assert.NotNull(exceptionType);
                    Assert.IsType(exceptionType, e);
                    Assert.Equal(pos, newPos);
                    Assert.Equal(endOffs, newEndOffs);
                }

                Assert.Equal(cleanBuff.AsSpan(0, checkPos),
                    buffWrap.Buffer.AsSpan(0, checkPos));
            }
        }
    }

    [InlineData("empty", 0x200, 0x100, null, "", "utf-8", -1, false)]
    [InlineData("empty filled", 0xf0, 0x100, null, "", "utf-8", 0x10, true, 0x100)]
    [InlineData("at start", 0, 0x100, null, "\rAa9-~\0", "ascii", -1, false, 7)]
    [InlineData("at start filled", 0, 0x100, null, "\rAa9-~\0", "ascii", 0x10, true, 0x10)]
    [InlineData("at end", 0xf9, 0x100, null, "\rAa9-~\0", "ascii", -1, false, 0x100)]
    [InlineData("UTF-8", 0xf5, 0x100, null, "~é€\U00024B62\0", "utf-8", -1, false, 0x100)]
    [InlineData("UTF-8 filled", 0xf0, 0x100, null, "~é€\U00024B62\0", "utf-8", 0x10, true, 0x100)]
    [InlineData("UTF-16BE", 0xee, 0x100, null, "~é€\U00024B62\ud7ff\ue000\uffff\0", "utf-16be", -1, false, 0x100)]
    [InlineData("Shift-JIS", 0xf5, 0x100, null, "Aｱあ亜漢①\0", "shift_jis", -1, false, 0x100)]
    [InlineData("filled past end", 0xf1, 0x100, false, "", "utf-8", 0x10, true, -1, -1, typeof(IOException))]
    [InlineData("invalid ASCII char", 0, 0x100, null, "\xff", "ascii", -1, false, -1, -1, typeof(EncoderFallbackException))]
    [InlineData("invalid Shift-JIS char", 0, 0x100, null, "𠮟", "shift_jis", -1, false, -1, -1, typeof(EncoderFallbackException))]
    [InlineData("oversized 1", 0, 0x100, null, "~é€\U00024B62\0", "utf-8", 8, false, -1, -1, typeof(ArgumentException))]
    [InlineData("oversized 2", 0, 0x100, null, "~é€\U00024B62\0", "utf-8", 8, true, -1, -1, typeof(ArgumentException))]
    [Theory]
    public void TestWriteString(
        string testName,
        int pos,
        int endOffs,
        bool? canGrow,
        string value,
        string encName,
        int maxBytes,
        bool fill,
        int tgtPos = -1,
        int tgtEndOffs = -1,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        // Shift-JIS support is not registered by default and needs this
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (tgtPos < 0)
            tgtPos = pos;
        if (tgtEndOffs < 0)
            tgtEndOffs = endOffs;

        var cleanBuff = CreateStandardBuffer();
        foreach (bool isResizeable in TestUtils.EnumBool(canGrow))
        {
            var buff = cleanBuff.ToArray();
            Encoding encoding = CreateEncoding(encName);

            try
            {
                int newPos = pos,
                    newEndOffs = endOffs;
                int size;

                if (maxBytes > 0)
                {
                    if (isResizeable)
                        size = Common.Write(
                            buff,
                            ref newPos,
                            ref newEndOffs,
                            GetGrowBuffer(isResizeable),
                            value,
                            encoding,
                            maxBytes,
                            fill);
                    else
                        size = Common.Write(
                            buff,
                            ref newPos,
                            newEndOffs,
                            value,
                            encoding,
                            maxBytes,
                            fill);
                }
                else
                {
                    if (isResizeable)
                        size = Common.Write(
                            buff,
                            ref newPos,
                            ref newEndOffs,
                            GetGrowBuffer(isResizeable),
                            value,
                            encoding);
                    else
                        size = Common.Write(
                            buff,
                            ref newPos,
                            newEndOffs,
                            value,
                            encoding);
                }

                Assert.Null(exceptionType);

                var bytes = encoding.GetBytes(value);
                int strSize = bytes.Length;
                int startOffs = Math.Min(pos, endOffs);

                Assert.Equal(tgtPos, newPos);
                Assert.Equal(tgtEndOffs, newEndOffs);

                Assert.Equal(cleanBuff.AsSpan(0, startOffs),
                    buff.AsSpan(0, startOffs));
                Assert.Equal(bytes, buff[startOffs..(startOffs + strSize)]);
                if (newPos <= endOffs)
                    Assert.Equal(
                        cleanBuff[newPos..endOffs], buff[newPos..endOffs]);

                if (maxBytes >= 0 && fill)
                    Assert.Equal(Enumerable.Repeat(
                        (byte)0, maxBytes - strSize).ToArray(),
                        buff[(startOffs + strSize)..(startOffs + size)]);
                else if (maxBytes < 0)
                    Assert.Equal(strSize, size);
            }
            catch (XunitException)
            { throw; }
            catch (Exception e)
            {
                Assert.NotNull(exceptionType);
                Assert.IsType(exceptionType, e);
            }
        }
    }

    [InlineData("negative pos", null, -1, 0, 0, 0, 0, 0, 0, typeof(TraceAssertException))]
    [InlineData("negative endOffs", null, 0, -1, 0, 0, 0, 0, 0, typeof(TraceAssertException))]
    [InlineData("endOffs past end", null, 0, 0x101, 0, 0, 0, 0, 0, typeof(TraceAssertException))]
    [InlineData("bad elementSize", null, 0, 0, 0, 0, 0, 0, 0, typeof(TraceAssertException))]
    [InlineData("negative bufferLength", null, 0, 0, 0, -1, 0, 0, 0, typeof(TraceAssertException))]
    [InlineData("negative count", null, 0, 0x100, 4, 64, 32, -1, 32)]
    //[InlineData("overflow 1", null, int.MaxValue, int.MaxValue, 1, int.MaxValue, -1, 1, 0, typeof(OverflowException))]
    [InlineData("overflow 2", null, int.MaxValue, 1, 1, int.MaxValue, int.MaxValue, 1, 0, typeof(OverflowException))]
    [InlineData("overflow 3", null, 1, 1, int.MaxValue, 0x100, 1, 1, 0, typeof(OverflowException))]
    [InlineData("fully specified", null, 0, 0x100, 4, 64, 32, 31, 31)]
    [InlineData("fully specified 2", null, 0, 0x100, 4, 64, 32, 32, 32)]
    [InlineData("read past buffer end", false, 0x100, 0x100, 4, 64, 32, 1, 0, typeof(EndOfStreamException))]
    [InlineData("write past buffer end", true, 0x100, 0x100, 4, 64, 32, 1, 1)]
    [InlineData("past out end", null, 0xfd, 0x100, 4, 64, 64, 1, 1, typeof(IndexOutOfRangeException))]
    [Theory]
    public void TestResolveCount(
        string testName,
        bool? isWrite,
        int pos,
        int endOffs,
        int elementSize,
        int bufferLength,
        int index,
        int count,
        int _count,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        foreach (bool write in TestUtils.EnumBool(isWrite))
        {
            try
            {
                var span = CreateStandardBuffer().AsSpan();
                int resCount;
                if (write)
                {
                    var spanCopy = span;
                    resCount = Common.ResolveWriteCount(
                        ref spanCopy,
                        pos,
                        endOffs,
                        (a, b) => new Span<byte>(),
                        elementSize,
                        bufferLength,
                        index,
                        count);
                }
                else
                {
                    resCount = Common.ResolveReadCount(
                        span,
                        pos,
                        endOffs,
                        elementSize,
                        bufferLength,
                        index,
                        count);
                }

                Assert.Null(exceptionType);
                Assert.Equal(_count, resCount);
            }
            catch (XunitException)
            { throw; }
            catch (Exception e)
            {
                Assert.NotNull(exceptionType);
                Assert.IsType(exceptionType, e);
            }
        }
    }

    [InlineData("negative pos", null, -1, 0, 0, 0, typeof(TraceAssertException))]
    [InlineData("negative size", null, 0, 0, -1, 0, typeof(TraceAssertException))]
    [InlineData("same size", null, 0xff, 0x100, 1, 0x100)]
    [InlineData("overflow", null, int.MaxValue, 0, 1, 0, typeof(OverflowException))]
    [InlineData("still fits", null, 0, 0, 0x100, 0x100)]
    [InlineData("grow", true, 0x200, 0, 0x100, 0x300)]
    [InlineData("grow fail", false, 0x200, 0, 0x100, 0x300, typeof(IOException))]
    [Theory]
    public void TestEnsureSpace(
        string testName,
        bool? isResizeable,
        int pos,
        int endOffs,
        int size,
        int _newSize,
        Type? exceptionType = null)
    {
        if (ShouldSkipAssert(exceptionType))
            return;

        foreach (bool canGrow in TestUtils.EnumBool(isResizeable))
        {
            try
            {
                var span = CreateStandardBuffer().AsSpan();
                int newSize = Common.EnsureSpace(
                    ref span,
                    pos,
                    endOffs,
                    GetGrowBuffer(canGrow),
                    size);

                Assert.Null(exceptionType);
                Assert.Equal(_newSize, newSize);
            }
            catch (XunitException)
            { throw; }
            catch (Exception e)
            {
                Assert.NotNull(exceptionType);
                Assert.IsType(exceptionType, e);
            }
        }
    }

    Common.GrowBufferDelegate GetGrowBuffer(bool isResizeable)
    {
        return isResizeable
            ? (newSize, endOffs) => new Span<byte>()
            : Common.DummyGrowBuffer;
    }

    bool ShouldSkipAssert(Type? exceptionType)
    {
        if (exceptionType == null
            || exceptionType != typeof(TraceAssertException))
            return false;
        else
#if DEBUG
            return false;
#else
            return true;
#endif
    }

    byte[] CreateStandardBuffer()
        => Enumerable.Range(0, 0x100).Select(x => (byte)x).ToArray();

    Encoding CreateEncoding(string encodingName)
    {
        return Encoding.GetEncoding(
            encodingName,
            new EncoderExceptionFallback(),
            new DecoderExceptionFallback());
    }
}
