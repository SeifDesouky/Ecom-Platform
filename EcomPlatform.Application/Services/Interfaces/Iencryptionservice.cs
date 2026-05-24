namespace EcomPlatform.Application.Common.Interfaces
{
    /// <summary>
    /// تشفير وفك تشفير القيم الحساسة (ApiKey, ApiSecret, Tokens)
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>تشفير نص — يرجع Base64 string أو null لو الدخل null</summary>
        string? Encrypt(string? plainText);

        /// <summary>فك تشفير نص — يرجع النص الأصلي أو null لو الدخل null</summary>
        string? Decrypt(string? cipherText);
    }
}