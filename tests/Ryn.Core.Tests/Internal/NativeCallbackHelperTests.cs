using FluentAssertions;
using Ryn.Core.Internal;
using Xunit;

namespace Ryn.Core.Tests.Internal;

public sealed class NativeCallbackHelperTests
{
    [Fact]
    public unsafe void Alloc_Resolve_Free_RoundTrip()
    {
        var target = new object();
        var handle = NativeCallbackHelper.Alloc(target);

        var resolved = NativeCallbackHelper.Resolve<object>(handle);

        resolved.Should().BeSameAs(target);

        NativeCallbackHelper.Free(handle);
    }

    [Fact]
    public unsafe void Resolve_ReturnsCorrectType()
    {
        var target = "test string";
        var handle = NativeCallbackHelper.Alloc(target);

        var resolved = NativeCallbackHelper.Resolve<string>(handle);

        resolved.Should().Be("test string");

        NativeCallbackHelper.Free(handle);
    }

    [Fact]
    public unsafe void Alloc_PreventsGarbageCollection()
    {
        var target = new byte[1024];
        var handle = NativeCallbackHelper.Alloc(target);

        GC.Collect(2, GCCollectionMode.Aggressive, true);
        GC.WaitForPendingFinalizers();

        var resolved = NativeCallbackHelper.Resolve<byte[]>(handle);
        resolved.Should().BeSameAs(target);

        NativeCallbackHelper.Free(handle);
    }
}
