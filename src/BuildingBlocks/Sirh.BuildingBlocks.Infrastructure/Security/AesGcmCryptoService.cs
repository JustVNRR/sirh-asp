using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;

namespace Sirh.BuildingBlocks.Infrastructure.Security;

public sealed class AesGcmCryptoService : IDataProtectorService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesGcmCryptoService(IOptions<EncryptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var masterKey = options.Value.MasterKey;
        if (string.IsNullOrWhiteSpace(masterKey) || masterKey.Length < EncryptionOptions.MinMasterKeyLength)
        {
            throw new InvalidOperationException(
                $"Encryption:MasterKey must be at least {EncryptionOptions.MinMasterKeyLength} characters long.");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(masterKey));
    }

    public string EncryptString(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = EncryptBytes(plainBytes);
        return Convert.ToBase64String(encrypted);
    }

    public string DecryptString(string cipherTextBase64)
    {
        ArgumentNullException.ThrowIfNull(cipherTextBase64);
        var cipherBytes = Convert.FromBase64String(cipherTextBase64);
        var plainBytes = DecryptBytes(cipherBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public byte[] EncryptBytes(ReadOnlySpan<byte> plainBytes)
    {
        var result = new byte[NonceSizeBytes + TagSizeBytes + plainBytes.Length];
        var nonce = result.AsSpan(0, NonceSizeBytes);
        var tag = result.AsSpan(NonceSizeBytes, TagSizeBytes);
        var cipherText = result.AsSpan(NonceSizeBytes + TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plainBytes, cipherText, tag);

        return result;
    }

    public byte[] DecryptBytes(ReadOnlySpan<byte> cipherBytes)
    {
        if (cipherBytes.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Encrypted payload is too short.");
        }

        var nonce = cipherBytes[..NonceSizeBytes];
        var tag = cipherBytes.Slice(NonceSizeBytes, TagSizeBytes);
        var cipherText = cipherBytes[(NonceSizeBytes + TagSizeBytes)..];

        var plainBytes = new byte[cipherText.Length];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Decrypt(nonce, cipherText, tag, plainBytes);

        return plainBytes;
    }
}
