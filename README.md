# BinaryBufferLib

A .NET library of lightweight objects for reading and writing binary data that prioritizes convenience. Access is stream-like, allowing for efficient serialization. Numerous value-added features such as automatic conversion between endians and reading and writing enumerables exist.

## Overview

Four classes designed for different use cases, forming two pairs:
- `DynamicBuffer`: A class with a self-resizing buffer that bytes and binary types can be read from and written to, intended for writing out entire files or variable-sized structures.
- `BufferReader`: A struct that wraps a `ReadOnlyMemory<byte>` and allows bytes and binary types to be read.
- `FixedBuffer` and `FixedBufferReader`: Smaller, simpler ref structs wrapping `(ReadOnly)Span<byte>`s that are closely analogous to `DynamicBuffer`/`BufferReader`, but lack some features that cannot be supported with ref structs. Intended for reading and writing individual, fixed-size structures.

Key features:
- `Stream`-like semantics with `Length`, `Position`, and `Seek`
- Additional list-like features such as `Capacity` and `EnsurceCapacity` (`DynamicBuffer` only), `this[]`
- `DynamicBuffer` implements the `IBufferWriter<byte>` interface for writing custom types
- Big, little, and native endian versions of all read/write functions exist
- Support for built-in integer and floating-point types
- Support for arrays through type[], `(ReadOnly)Span`, `I(ReadOnly)List`, `IEnumerable` (reading arrays as `IEnumerables` is only supported for `BufferReader`)

To do:
- Needs more documentation
- Needs more unit tests

## Usage

[Example](./Example/Program.cs)

## FAQ

### Why?

Because every alternative left something to be desired. `BinaryPrimitives` provided the mechanism for reading, writing, and endian conversion, but is intolerably verbose. `BinaryReader`/`BinaryWriter` have the right idea in general, but either require manual endian conversion or place constraints on the format of the data; additionally, they were relatively heavy as they sit on top of a `Stream`. A number of other packages exist with similar functionality, but all lacked at least one useful feature.

Ultimately BinaryBufferLib was created to answer the question "what is the easiest way to read and write arbitrary binary packets and files with no constraints on the format of that data?"

### Why a stream-like interface?

This is generally the most efficient way to read/write objects. The alternative would be to provide an offset to read/write the specified item at, but this becomes very cumbersome very quickly, requiring numerous hard-coded offsets to structure members.

### Why does reading an array as an `IEnumerable` not update `Position`?

This is to allow peek semantics without requiring manual rollback of the current position. `Advance` can be used to advance the position after the number of items to advance is known.

### What features might be added in the future?
Some possibilities:
- A per-buffer property that specifies the default endian so the endian suffixes can be omitted from functions
- Support for strings
- Support for specialized formats such as 7-bit integers
- `Stream` versions that provide the same features in the form of wrappers around a provided `Stream`
- A source generator to automatically create read/write code for custom structures
- Implement Try versions of functions that don't throw on failure
