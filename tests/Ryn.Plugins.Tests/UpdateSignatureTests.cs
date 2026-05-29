using System.Text;
using FluentAssertions;
using Ryn.Plugins.Updater;
using Xunit;

namespace Ryn.Plugins.Tests;

public sealed class UpdateSignatureTests
{
    [Fact]
    public void Verify_AcceptsGenuineSignature()
    {
        var (pub, priv) = UpdateSignature.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("ryn update payload v1.2.3");

        var sig = UpdateSignature.Sign(data, priv);

        UpdateSignature.Verify(data, sig, pub).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsTamperedPayload()
    {
        var (pub, priv) = UpdateSignature.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("genuine payload");
        var sig = UpdateSignature.Sign(data, priv);

        var tampered = Encoding.UTF8.GetBytes("genuine payloac"); // one byte changed
        UpdateSignature.Verify(tampered, sig, pub).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsSignatureFromDifferentKey()
    {
        var (_, attackerPriv) = UpdateSignature.GenerateKeyPair();
        var (trustedPub, _) = UpdateSignature.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("payload");

        var attackerSig = UpdateSignature.Sign(data, attackerPriv);

        // Signed by the attacker's key, verified against the app's trusted key -> rejected.
        UpdateSignature.Verify(data, attackerSig, trustedPub).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_OnMalformedInputs()
    {
        var data = Encoding.UTF8.GetBytes("x");
        UpdateSignature.Verify(data, [1, 2, 3], "not-base64!!!").Should().BeFalse();
        UpdateSignature.Verify(data, [1, 2, 3], "").Should().BeFalse();
        var (pub, _) = UpdateSignature.GenerateKeyPair();
        UpdateSignature.Verify(data, [9, 9, 9], pub).Should().BeFalse(); // junk signature
    }

    [Fact]
    public async Task Download_RefusesWithoutPublicKey()
    {
        using var service = new UpdaterService(new UpdaterOptions
        {
            GitHubOwner = "o",
            GitHubRepo = "r",
            PublicKey = null, // not configured
        });

        var info = new UpdateInfo
        {
            Version = "1.0.0",
            ReleaseUrl = new Uri("https://github.com/o/r/releases"),
            AssetUrl = new Uri("https://github.com/o/r/releases/download/v1/app.zip"),
            SignatureUrl = new Uri("https://github.com/o/r/releases/download/v1/app.zip.sig"),
        };

        // Must refuse before any network activity because there is no key to verify against.
        var act = async () => await service.DownloadUpdateAsync(info);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Download_RefusesWhenSignatureAssetMissing()
    {
        var (pub, _) = UpdateSignature.GenerateKeyPair();
        using var service = new UpdaterService(new UpdaterOptions
        {
            GitHubOwner = "o",
            GitHubRepo = "r",
            PublicKey = pub,
        });

        var info = new UpdateInfo
        {
            Version = "1.0.0",
            ReleaseUrl = new Uri("https://github.com/o/r/releases"),
            AssetUrl = new Uri("https://github.com/o/r/releases/download/v1/app.zip"),
            SignatureUrl = null, // no detached signature published
        };

        var act = async () => await service.DownloadUpdateAsync(info);
        await act.Should().ThrowAsync<System.Security.SecurityException>();
    }

    [Fact]
    public async Task Apply_RejectsUnknownHandle()
    {
        var (pub, _) = UpdateSignature.GenerateKeyPair();
        using var service = new UpdaterService(new UpdaterOptions
        {
            GitHubOwner = "o", GitHubRepo = "r", PublicKey = pub,
        });

        // A handle never produced by a verified download — the old JS-controlled-path RCE vector.
        var act = async () => await service.ApplyUpdateAsync("deadbeefdeadbeefdeadbeefdeadbeef");
        await act.Should().ThrowAsync<System.Security.SecurityException>();
    }
}
