namespace Sirh.BuildingBlocks.Application.Abstractions;

public interface IDataProtectorService
{
    string EncryptString(string plainText);
    string DecryptString(string cipherTextBase64);
    byte[] EncryptBytes(ReadOnlySpan<byte> plainBytes);
    byte[] DecryptBytes(ReadOnlySpan<byte> cipherBytes);
}
