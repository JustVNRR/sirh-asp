using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Shouldly;
using Sirh.BuildingBlocks.Infrastructure.Security;
using Xunit;

namespace Sirh.UnitTests.Security;

public sealed class AesGcmCryptoServiceTests
{
    private const string ValidMasterKey = "SuperSecretMasterKeyForUnitTests123456!";

    private static AesGcmCryptoService CreateService(string key = ValidMasterKey) =>
        new(Options.Create(new EncryptionOptions { MasterKey = key }));

    [Fact]
    public void Constructor_Should_Throw_When_MasterKey_Is_Too_Short()
    {
        Should.Throw<InvalidOperationException>(() => CreateService("too-short"));
    }

    [Fact]
    public void EncryptString_And_DecryptString_Should_RoundTrip_And_Use_Random_Nonce()
    {
        var sut = CreateService();
        const string plainText = "Jean Dupont - 06 12 34 56 78";

        var cipher1 = sut.EncryptString(plainText);
        var cipher2 = sut.EncryptString(plainText);

        cipher1.ShouldNotBe(plainText);
        cipher1.ShouldNotBe(cipher2);
        sut.DecryptString(cipher1).ShouldBe(plainText);
        sut.DecryptString(cipher2).ShouldBe(plainText);
    }

    [Fact]
    public void DecryptBytes_Should_Throw_When_Payload_Is_Tampered()
    {
        var sut = CreateService();
        var encrypted = sut.EncryptBytes(Encoding.UTF8.GetBytes("Confidential contract"));
        encrypted[^1] ^= 0xFF;

        Should.Throw<CryptographicException>(() => sut.DecryptBytes(encrypted));
    }

    [Fact]
    public void DecryptBytes_Should_Throw_When_Key_Differs()
    {
        var encryptor = CreateService(ValidMasterKey);
        var decryptor = CreateService("AnotherMasterKeyWithDifferentSecretValue!");

        var encrypted = encryptor.EncryptBytes("Sensitive data"u8);

        Should.Throw<CryptographicException>(() => decryptor.DecryptBytes(encrypted));
    }
}
