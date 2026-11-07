using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace BinaryBufferLib.Test;

using Buffer = byte[];

public class TestDynamicBuffer
{
    static readonly bool[] BoolValues = [false, true];
    static readonly HashSet<Type> SignedTypes = [typeof(SByte), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Int128)];
    static readonly UInt64[] StandardArray8 = [0xfc, 0xfd, 0xfe, 0xff];
    static readonly UInt64[] StandardArray16 = [0xf8f9, 0xfafb, 0xfcfd, 0xfeff];
    static readonly UInt64[] StandardArray32 = [0xf0f1f2f3, 0xf4f5f6f7, 0xf8f9fafb, 0xfcfdfeff];
    static readonly UInt64[] StandardArray64 = [0xe0e1e2e3e4e5e6e7, 0xe8e9eaebecedeeef, 0xf0f1f2f3f4f5f6f7, 0xf8f9fafbfcfdfeff];

    [InlineData("dynamic", 16, 16, 0)]
    [InlineData("dynamic with auto-size", -1, 0x200, 0)]
    [Theory]
    public void TestConstructorSuccess(
        string testName,
        int dataSize,
        int _buffSize,
        int _endOffs)
    {
        DynamicBuffer buff = new(dataSize);

        Assert.Equal(_buffSize, buff.Capacity);
        Assert.Equal(_endOffs, buff.Length);
    }

    [InlineData("bigger", 0x201, 0x400)]
    [Theory]
    public void TestGrowBuffer(
        string testName,
        int minSize, 
        int _newSize)
    {
        var buff = CreateStandardBuffer(true);
        var initBuff = buff.AsSpan();
        var newBuff = buff.GrowBuffer(minSize, buff.Length);

        Assert.Equal(_newSize, newBuff.Length);
        Assert.Equal(initBuff, newBuff.Slice(0, initBuff.Length));
        Assert.Equal(
            Enumerable.Repeat<byte>(0, _newSize - initBuff.Length).ToArray(), 
            newBuff.Slice(initBuff.Length));
    }

    [InlineData("same size", -1)]
    [InlineData("shrink", 0x80)]
    [InlineData("too small", 0x7f, typeof(ArgumentOutOfRangeException))]
    [InlineData("grow", 0x1000)]
    [Theory]
    public void TestSetCapacity(
        string testName,
        int newCapacity,
        Type? exceptionType = null)
    {
        var buff = CreateStandardBuffer(true);

        if (newCapacity < 0)
            newCapacity = buff.Capacity;

        var prevBuff = buff.AsSpan().ToArray();
        if (exceptionType is null)
        {
            buff.Capacity = newCapacity;
            Assert.Equal(newCapacity, buff.Capacity);
        }
        else
        {
            int prevCapacity = buff.Capacity;
            Assert.Throws(exceptionType, () => buff.Capacity = newCapacity);
            Assert.Equal(prevCapacity, buff.Capacity);
        }

        Assert.Equal(prevBuff.Length, buff.Length);
        Assert.Equal(prevBuff, buff.AsSpan().ToArray());
    }

    [InlineData("negative index", -1, 0, 0, typeof(IndexOutOfRangeException))]
    [InlineData("start", 0, 0x40, 0xff)]
    [InlineData("end", 0x7f, 0xbf, 0xff)]
    [InlineData("past end", 0x80, 0, 0, typeof(IndexOutOfRangeException))]
    [Theory]
    public void TestIndex(
        string testName,
        int index,
        int prevValue,
        int newValue,
        Type? exceptionType = null)
    {
        foreach (bool isResizeable in BoolValues)
        {
            var buff = CreateStandardBuffer(isResizeable);
            
            if (exceptionType is null)
            {
                Assert.Equal(prevValue, buff[index]);
                buff[index] = (byte)newValue;
                Assert.Equal(newValue, buff[index]);
            }
            else
            {
                Assert.Throws(exceptionType, () => buff[index]);
                Assert.Throws(exceptionType, () => buff[index] = (byte)newValue);
            }
        }
    }

    [InlineData("negative", null, -1, typeof(ArgumentOutOfRangeException))]
    [InlineData("length", null, 0x80)]
    [InlineData("smaller", null, 1)]
    [InlineData("larger", true, 0x300)]
    [Theory]
    public void TestEnsureCapacity(
        string testName,
        bool? canResize,
        int capacity,
        Type? exceptionType = null)
    {
        foreach (bool isResizeable in TestUtils.EnumBool(canResize))
        {
            var buff = CreateStandardBuffer(isResizeable);
            var prevBuff = buff.AsSpan().ToArray();

            if (exceptionType is null)
            {
                int newCapacity = buff.EnsureCapacity(capacity);

                Assert.True(newCapacity >= capacity);
            }
            else
                Assert.Throws(exceptionType, 
                    () => buff.EnsureCapacity(capacity));

            Assert.Equal(prevBuff.Length, buff.Length);
            Assert.Equal(prevBuff, buff.AsSpan().ToArray());
        }
    }

    [InlineData("negative offset from start", null, -1, SeekOrigin.Begin, 64, typeof(ArgumentOutOfRangeException))]
    [InlineData("start from start", null, 0, SeekOrigin.Begin, 0)]
    [InlineData("end from start", null, 128, SeekOrigin.Begin, 128)]
    [InlineData("negative offset from current", null, -65, SeekOrigin.Current, 64, typeof(ArgumentOutOfRangeException))]
    [InlineData("start from current", null, -64, SeekOrigin.Current, 0)]
    [InlineData("current from current", null, 0, SeekOrigin.Current, 64)]
    [InlineData("end from current", null, 64, SeekOrigin.Current, 128)]
    [InlineData("negative offset from end", null, -129, SeekOrigin.End, 64, typeof(ArgumentOutOfRangeException))]
    [InlineData("start from end", null, -128, SeekOrigin.End, 0)]
    [InlineData("end from end", null, 0, SeekOrigin.End, 128)]
    [Theory]
    public void TestSeek(
        string testName,
        bool? canResize,
        int offset,
        SeekOrigin origin,
        int result,
        Type? exceptionType = null)
    {
        foreach (bool isResizeable in TestUtils.EnumBool(canResize))
        {
            DynamicBuffer buff = CreateStandardBuffer(isResizeable, 64);
            if (exceptionType is null)
            {
                int newPos = buff.Seek(offset, origin);
                Assert.Equal(result, newPos);
            }
            else
                Assert.Throws(exceptionType, () => buff.Seek(offset, origin));

            Assert.Equal(result, buff.Position);
        }
    }

    DynamicBuffer CreateStandardBuffer(bool isResizeable, int offset = 0)
    {
        const int dataStart = 0x40, dataSize = 0x80;

        DynamicBuffer buff;
        //if (isResizeable)
        {
            buff = new(0x100);
            buff._endOffs = dataSize;
        }
        /*else
            Debug.Assert(false);
            buff = new(new byte[dataSize]);*/

        for (int i = 0; i < dataSize; i++)
            buff._buffer[i] = (byte)(i + dataStart);

        buff.Position = offset;

        return buff;
    }
}
