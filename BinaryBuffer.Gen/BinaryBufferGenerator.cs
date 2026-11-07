using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Buffers.Binary;
using System.Diagnostics;


//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Reflection.Metadata;
using System.Text;

namespace BinaryBuffer.Gen
{
    enum EncType
    {
        Little,
        Big,
        Native,
    }

    enum BufferType
    {
        Dynamic,
        FixedReader,
        FixedWriter,
    }

    enum MethodClass
    {
        Peek,
        Read,
        Write,
    }

    [Generator]
#pragma warning disable RS1042 // Implementations of this interface are not allowed
    public class BinaryBufferGenerator : ISourceGenerator
#pragma warning restore RS1042 // Implementations of this interface are not allowed
    {
        public void Initialize(GeneratorInitializationContext context)
        {
/*#if DEBUG
            if (!Debugger.IsAttached)
                Debugger.Launch();
#endif*/
        }

        public void Execute(GeneratorExecutionContext context)
        {
            GenerateEncodings(context);
            GenerateFixedBufferExtensions(context);
            GenerateBufferReaderMethods(context);
            GenerateDynamicBufferMethods(context);
        }

        void GenerateEncodings(GeneratorExecutionContext context)
        {
            StringBuilder sb = new($@"
using System;
using System.Buffers.Binary;

namespace BinaryBufferLib.Encodings
{{
");

            string indent = "    ";
            GenerateEncoding(sb, indent, "Int16");
            GenerateEncoding(sb, indent, "Int32");
            GenerateEncoding(sb, indent, "Int64");
            //GenerateEncoding(sb, indent, "Int128");
            //GenerateEncoding(sb, indent, "Half", false/*, "Int16"*/);

#if !NETSTANDARD
            GenerateEncoding(sb, indent, "Single", false/*, "Int32"*/);
            GenerateEncoding(sb, indent, "Double", false/*, "Int64"*/);
#endif

            sb.Append(
$@"
}}
");

            context.AddSource("Encodings.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        void GenerateFixedBufferExtensions(GeneratorExecutionContext context)
        {
            StringBuilder sb = new($@"
using System;
using System.Collections.Generic;
using System.IO;
using BinaryBufferLib.Encodings;

namespace BinaryBufferLib
{{
    public static class FixedBufferExtensions
    {{
");

            GenerateAccessMethods(
                sb, "    ", false, MethodClass.Read, false);
            GenerateAccessMethods(
                sb, "    ", false, MethodClass.Peek, false);
            GenerateAccessMethods(
                sb, "    ", false, MethodClass.Write, false);

            sb.Append(
$@"
    }}
}}
");

            context.AddSource("FixedBufferExtensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        void GenerateBufferReaderMethods(GeneratorExecutionContext context)
        {
            StringBuilder sb = new($@"
using System;
using System.Collections.Generic;
using System.IO;
using BinaryBufferLib.Encodings;

namespace BinaryBufferLib
{{
    public partial struct BufferReader
    {{
");

            GenerateAccessMethods(sb, "    ", true, MethodClass.Read, false);
            GenerateAccessMethods(sb, "    ", true, MethodClass.Peek, false);

            sb.Append(
$@"
    }}
}}
");

            context.AddSource("BufferReader.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        void GenerateDynamicBufferMethods(GeneratorExecutionContext context)
        {
            StringBuilder sb = new($@"
using System;
using System.Collections.Generic;
using System.IO;
using BinaryBufferLib.Encodings;

namespace BinaryBufferLib
{{
    public partial class DynamicBuffer
    {{
");

            GenerateAccessMethods(sb, "    ", true, MethodClass.Write, true);

            sb.Append(
$@"
    }}
}}
");

            context.AddSource("DynamicBuffer.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        void GenerateEncoding(
            StringBuilder sb,
            string baseIndent,
            string type,
            bool hasUnsigned = true)
        {
            for (int isUnsignedIdx = 0; isUnsignedIdx <= 1; isUnsignedIdx++)
            {
                if (!hasUnsigned && isUnsignedIdx != 0)
                    break;

                string augType = isUnsignedIdx != 0 ? "U" + type : type;

                for (int endianIdx = 0; endianIdx <= 2; endianIdx++)
                {
                    string shortSuff, readCall, writeBody;
                    if (endianIdx < 2)
                    {
                        bool isBig = endianIdx == 1;
                        string rawType = "";

                        shortSuff = isBig ? "BE" : "LE";
                        string longSuff = (isBig ? "Big" : "Little") + "Endian";
                        readCall = rawType != ""
                            ? $"BitConverter.{rawType}BitsTo{type}(BinaryPrimitives.Read{rawType}{longSuff}(buffer))"
                            : $"BinaryPrimitives.Read{augType}{longSuff}(buffer)";
                        writeBody = rawType != ""
                            ? $"    => BinaryPrimitives.Write{rawType}{longSuff}(buffer, BitConverter.{augType}To{rawType}Bits(value));"
                            : $"    => BinaryPrimitives.Write{augType}{longSuff}(buffer, value);";
                    }
                    else
                    {
                        shortSuff = "NE";
                        readCall = $"(BitConverter.IsLittleEndian ? BinaryPrimitives.Read{augType}LittleEndian(buffer) : BinaryPrimitives.Read{augType}BigEndian(buffer))"; 
                        writeBody = $@"{{ 
{baseIndent}        if (BitConverter.IsLittleEndian) 
{baseIndent}            BinaryPrimitives.Write{augType}LittleEndian(buffer, value);       
{baseIndent}        else 
{baseIndent}            BinaryPrimitives.Write{augType}BigEndian(buffer, value); 
{baseIndent}    }}";
                    }

                    sb.Append($@"
{baseIndent}internal struct {augType}{shortSuff}Encoding : IBinaryEncoding<{augType}>
{baseIndent}{{
{baseIndent}    public int Size => sizeof({augType});

{baseIndent}    public readonly {augType} Read(ReadOnlySpan<byte> buffer)
{baseIndent}        => {readCall};

{baseIndent}    public readonly void Write(Span<byte> buffer, {augType} value)
{baseIndent}    {writeBody}
{baseIndent}}}
");
                }
            }
        }

        void GenerateAccessMethods(
            StringBuilder sb,
            string baseIndent,
            bool isDynamic,
            MethodClass cls,
            bool hasEndOffs)
        {
            string indent = "    ";
            GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Byte", false, "S");
            GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Int16", true, "U");
            GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Int32", true, "U");
            GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Int64", true, "U");
            //GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Int128", true, "U");
            //GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Half", true, "");

#if !NETSTANDARD
            GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Single", true, "");
            GenerateAccessMethods(sb, indent, isDynamic, cls, hasEndOffs, "Double", true, "");
#endif
        }

        void GenerateAccessMethods(
            StringBuilder sb,
            string baseIndent,
            bool isDynamic,
            MethodClass cls,
            bool hasEndOffs,
            string type,
            bool hasEndians,
            string altPrefix)
        {
            string staticWord = "", thisParam = "", thisParamComma = "", thisPre = "", thisParamHelp = "", buffer, endArg, endGrowArgs;
            string readName, advValue, advSuff;

            (readName, advValue, advSuff) = cls != MethodClass.Peek
                ? ("Read", "true", "")
                : ("Peek", "false", " without advancing");
            
            if (!isDynamic)
            {
                string ioSuff = cls == MethodClass.Write 
                    ? "Writer" 
                    : "Reader";
                staticWord = "static ";
                thisParam = $"this ref FixedBuffer{ioSuff} buffer";
                thisParamComma = thisParam + ", ";
                thisParamHelp = "/// <param name=\"buffer\">The buffer to read into.</param>\r\n        ";
                thisPre = "buffer.";
                buffer = $"{thisPre}_buffer";
                endArg = $"{buffer}.Length";
                endGrowArgs = endArg;
            }
            else
            {
                buffer = hasEndOffs ? "_buffer" : "_buffer.Span";
                endArg = hasEndOffs ? "_endOffs" : "_buffer.Length";
                endGrowArgs = $"ref {endArg}, _growBuffer";
            }

            for (int endianIdx = 0; endianIdx <= 2; endianIdx++)
            {
                string endianText = "", endianSuff = "";
                if (hasEndians)
                {
                    bool isBig = endianIdx != 0;
                    if (endianIdx < 2)
                    {
                        endianText = (isBig ? "big" : "little") + "-endian ";
                        endianSuff = isBig ? "BE" : "LE";
                    }
                    else
                    {
                        endianText = "native-endian";
                        endianSuff = "NE";
                    }
                }
                else if (endianIdx != 0)
                    break;

                for (int altIdx = 0; altIdx <= 1; altIdx++)
                {
                    if (altIdx != 0 && altPrefix == "")
                        break;

                    string fullType = (altIdx != 0 ? altPrefix : "") + type,
                        endianType = fullType + endianSuff,
                        encoding = $"{fullType}{endianSuff}Encoding";

                    if (cls != MethodClass.Write)
                    {
                        if (cls == MethodClass.Read && isDynamic)
                        {
                            sb.Append($@"
        /// <summary>
        /// Get an enumerable to read {endianText}{fullType} values beginning from the current position. The enumerable has its own position, and does not advance the position of the DynamicBuffer.
        /// </summary>
        public IEnumerable<{fullType}> Read{endianType}s()
            => Enumerate<{fullType}, {encoding}>();

");
                        }

                        sb.Append($@"
        /// <summary>
        /// Read a {endianText}{fullType} from the current position{advSuff}.
        /// </summary>
        {thisParamHelp}/// <returns>The value read.</returns>
        /// <exception cref=""EndOfStreamException"">The buffer did not contain a full value.</exception>
        public {staticWord}{fullType} {readName}{endianType}({thisParam})
            => Common.Read<{fullType}, {encoding}>({buffer}, ref {thisPre}_pos, {endArg}, {advValue});

        /// <summary>
        /// Read multiple {endianText}{fullType}s from the current position into a buffer{advSuff}.
        /// </summary>
        {thisParamHelp}/// <param name=""values"">The buffer to read into.</param>
        /// <param name=""index"">The initial buffer index to read into.</param>
        /// <param name=""count"">The number of values to read, or -1 if the buffer should be filled to the end. If specified, exactly that many values will be read.</param>
        /// <returns>The number of values read.</returns>
        /// <exception cref=""EndOfStreamException"">The buffer did not contain the number of values requested.</exception>
        public {staticWord}int {readName}{endianSuff}({thisParamComma}IList<{fullType}> values, int index = 0, int count = -1)
            => Common.Read<{fullType}, {encoding}, IList<{fullType}>>({buffer}, ref {thisPre}_pos, {endArg}, values, index, count, {advValue});

        /// <summary>
        /// Read multiple {endianText}{fullType}s from the current position into a buffer{advSuff}.
        /// </summary>
        {thisParamHelp}/// <param name=""values"">The buffer to read into.</param>
        /// <param name=""index"">The initial buffer index to read into.</param>
        /// <param name=""count"">The number of values to read, or -1 if the buffer should be filled to the end. If specified, exactly that many values will be read.</param>
        /// <returns>The number of values read.</returns>
        /// <exception cref=""EndOfStreamException"">The buffer did not contain the number of values requested.</exception>
        public {staticWord}int {readName}{endianSuff}({thisParamComma}Span<{fullType}> values, int index = 0, int count = -1)
            => Common.Read<{fullType}, {encoding}>({buffer}, ref {thisPre}_pos, {endArg}, values, index, count, {advValue});

");
                    }
                    else
                    {
                        sb.Append($@"
        /// <summary>
        /// Write a {endianText}{fullType} at the current position.
        /// </summary>
        {thisParamHelp}/// <param name=""value"">The value to write.</param>
        public {staticWord}void Write{endianSuff}({thisParamComma}{fullType} value)
            => Common.Write<{fullType}, {encoding}>({buffer}, ref {thisPre}_pos, {endGrowArgs}, value);

        /// <summary>
        /// Write some number of a {endianText}{fullType} to the current position.
        /// </summary>
        {thisParamHelp}/// <param name=""value"">The value to write.</param>
        /// <param name=""count"">The number to write.</param>
        public {staticWord}void Write{endianSuff}({thisParamComma}{fullType} value, int count)
            => Common.Write<{fullType}, {encoding}>({buffer}, ref {thisPre}_pos, {endGrowArgs}, value, count);

        /// <summary>
        /// Write multiple {endianText}{fullType}s from an enumerable to the current position.
        /// </summary>
        {thisParamHelp}/// <param name=""values"">The enumerable to read from.</param>
        /// <returns>The number of values written.</returns>
        public {staticWord}int Write{endianSuff}({thisParamComma}IEnumerable<{fullType}> values)
            => Common.Write<{fullType}, {encoding}, IEnumerable<{fullType}>>({buffer}, ref {thisPre}_pos, {endGrowArgs}, values);

        /// <summary>
        /// Write multiple {endianText}{fullType}s from a buffer to the current position.
        /// </summary>
        {thisParamHelp}/// <param name=""values"">The buffer to read from.</param>
        /// <param name=""index"">The initial buffer index to read from.</param>
        /// <param name=""count"">The number of values to write, or -1 to write all values in the buffer. If specified, exactly that many values will be written.</param>
        /// <returns>The number of values written.</returns>
        public {staticWord}int Write{endianSuff}({thisParamComma}IReadOnlyList<{fullType}> values, int index = 0, int count = -1)
            => Common.Write<{fullType}, {encoding}>({buffer}, ref {thisPre}_pos, {endGrowArgs}, values, index, count);

        /// <summary>
        /// Write multiple {endianText}{fullType}s from a buffer to the current position.
        /// </summary>
        {thisParamHelp}/// <param name=""values"">The buffer to read from.</param>
        /// <param name=""index"">The initial buffer index to read from.</param>
        /// <param name=""count"">The number of values to write, or -1 to write all values in the buffer. If specified, exactly that many values will be written.</param>
        /// <returns>The number of values written.</returns>
        public {staticWord}int Write{endianSuff}({thisParamComma}ReadOnlySpan<{fullType}> values, int index = 0, int count = -1)
            => Common.Write<{fullType}, {encoding}>({buffer}, ref {thisPre}_pos, {endGrowArgs}, values, index, count);

");
                    }
                }
            }
        }
    }
}
