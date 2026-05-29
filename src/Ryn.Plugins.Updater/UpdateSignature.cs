using System.Security.Cryptography;

namespace Ryn.Plugins.Updater;

/// <summary>
/// Detached-signature primitives for the auto-updater. Uses ECDSA over the NIST P-256 curve with
/// SHA-256 and DER-encoded signatures — all from the BCL, so there is no third-party dependency and
/// it is fully NativeAOT-safe (no reflection). This plays the same role minisign/ed25519 plays for
/// Tauri: an update is only ever applied if a detached signature over its exact bytes verifies against
/// a public key embedded in the application.
/// </summary>
public static class UpdateSignature
{
    private const DSASignatureFormat SignatureFormat = DSASignatureFormat.Rfc3279DerSequence;

    /// <summary>
    /// Verifies <paramref name="signature"/> over <paramref name="data"/> against a base64-encoded
    /// X.509 SubjectPublicKeyInfo (SPKI) public key. Returns false on any malformed input rather than
    /// throwing, so callers can treat "could not verify" identically to "verification failed".
    /// </summary>
    public static bool Verify(byte[] data, byte[] signature, string publicKeyBase64)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);
        if (string.IsNullOrEmpty(publicKeyBase64))
            return false;

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, SignatureFormat);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Generates a fresh keypair. The public key (embed in the app) and private key (keep secret) are base64 SPKI/PKCS#8.</summary>
    public static (string PublicKeyBase64, string PrivateKeyBase64) GenerateKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        var priv = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
        return (pub, priv);
    }

    /// <summary>Signs <paramref name="data"/> with a base64 PKCS#8 private key, returning the detached signature bytes (publish as the <c>.sig</c> asset, base64-encoded).</summary>
    public static byte[] Sign(byte[] data, string privateKeyBase64)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(privateKeyBase64);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256, SignatureFormat);
    }
}
