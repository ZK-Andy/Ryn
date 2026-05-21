using FluentAssertions;
using Ryn.Core.Internal;
using Xunit;

namespace Ryn.Core.Tests.Internal;

public sealed class Utf8StringTests
{
    [Fact]
    public unsafe void Create_SimpleAscii_RoundTrips()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create("hello", buf);

        var result = Utf8String.ToManaged(str.Pointer);

        result.Should().Be("hello");
        str.Dispose();
    }

    [Fact]
    public unsafe void Create_EmptyString_ReturnsValidPointer()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create("", buf);

        str.ByteCount.Should().Be(0);
        str.Dispose();
    }

    [Fact]
    public unsafe void Create_UnicodeString_RoundTrips()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create("Hello, 世界! 🌍", buf);

        var result = Utf8String.ToManaged(str.Pointer);

        result.Should().Be("Hello, 世界! 🌍");
        str.Dispose();
    }

    [Fact]
    public unsafe void Create_LongString_UsesPooledBuffer()
    {
        var longString = new string('x', 512);
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create(longString, buf);

        var result = Utf8String.ToManaged(str.Pointer);

        result.Should().Be(longString);
        str.Dispose();
    }

    [Fact]
    public unsafe void ToManaged_NullPointer_ReturnsEmpty()
    {
        var result = Utf8String.ToManaged((sbyte*)null);
        result.Should().BeEmpty();
    }

    [Fact]
    public unsafe void ToManaged_WithLength_ReturnsSubstring()
    {
        Span<byte> buf = stackalloc byte[256];
        var str = Utf8String.Create("hello world", buf);

        var result = Utf8String.ToManaged(str.Pointer, 5);

        result.Should().Be("hello");
        str.Dispose();
    }
}
