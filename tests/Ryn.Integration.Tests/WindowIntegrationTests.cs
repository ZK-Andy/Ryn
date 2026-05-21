using FluentAssertions;
using Ryn.Core.Internal;
using Ryn.Interop;
using Xunit;

namespace Ryn.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class WindowIntegrationTests
{
    [Fact]
    public unsafe void NativeLibrary_Loads_AndReturnsVersion()
    {
        NativeLibraryResolver.Register();

        var versionPtr = Saucer.saucer_version();

        Assert.True(versionPtr != null, "saucer_version returned null");
        var version = Utf8String.ToManaged(versionPtr);
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public unsafe void Stash_CreateEmpty_AndFree()
    {
        NativeLibraryResolver.Register();

        var stash = Saucer.saucer_stash_new_empty();
        Assert.True(stash != null, "saucer_stash_new_empty returned null");

        var size = Saucer.saucer_stash_size(stash);
        size.Should().Be((nuint)0);

        Saucer.saucer_stash_free(stash);
    }

    [Fact]
    public unsafe void Stash_FromString_RoundTrips()
    {
        NativeLibraryResolver.Register();

        Span<byte> buf = stackalloc byte[256];
        var input = Utf8String.Create("hello saucer", buf);
        var stash = Saucer.saucer_stash_new_from_str(input.Pointer);
        input.Dispose();

        Assert.True(stash != null, "saucer_stash_new_from_str returned null");

        var size = Saucer.saucer_stash_size(stash);
        size.Should().BeGreaterThan((nuint)0);

        var data = Saucer.saucer_stash_data(stash);
        var result = Utf8String.ToManaged((sbyte*)data, (int)size);
        result.Should().Be("hello saucer");

        Saucer.saucer_stash_free(stash);
    }

    [Fact]
    public unsafe void Url_Parse_AndReadBack()
    {
        NativeLibraryResolver.Register();

        Span<byte> buf = stackalloc byte[256];
        var input = Utf8String.Create("https://example.com/path", buf);
        int error = 0;
        var url = Saucer.saucer_url_new_parse(input.Pointer, &error);
        input.Dispose();

        Assert.True(url != null, "saucer_url_new_parse returned null");
        error.Should().Be(0);

        var scheme = SaucerStringReader.ReadUrlScheme(url);
        scheme.Should().Be("https");

        var path = SaucerStringReader.ReadUrlPath(url);
        path.Should().Be("/path");

        Saucer.saucer_url_free(url);
    }
}
