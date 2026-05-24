using EcomPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace EcomPlatform.Infrastructure.Services
{
    /// <summary>
    /// تشفير AES-256-GCM — authenticated encryption بدون padding oracle
    /// Format المحفوظ في DB: Base64( nonce[12] + tag[16] + ciphertext )
    /// </summary>
    public sealed class AesEncryptionService : IEncryptionService
    {
        // AES-GCM constants
        private const int NonceSize = 12;   // 96-bit — recommended for GCM
        private const int TagSize = 16;   // 128-bit authentication tag

        private readonly byte[] _key;

        public AesEncryptionService(IConfiguration configuration)
        {
            var base64Key = configuration["Encryption:Key"]
                ?? throw new InvalidOperationException(
                    "Encryption:Key is missing from configuration. " +
                    "Add a 32-byte Base64 key under Encryption:Key in appsettings.");

            _key = Convert.FromBase64String(base64Key);

            if (_key.Length != 32)
                throw new InvalidOperationException(
                    $"Encryption:Key must be exactly 32 bytes (256 bits). Got {_key.Length} bytes.");
        }

        /// <inheritdoc/>
        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            // nonce جديد لكل عملية تشفير — ده ضروري لـ GCM
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipher = new byte[plainBytes.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plainBytes, cipher, tag);

            // دمج: nonce + tag + ciphertext في buffer واحد
            var result = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);

            return Convert.ToBase64String(result);
        }

        /// <inheritdoc/>
        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            byte[] data;
            try
            {
                data = Convert.FromBase64String(cipherText);
            }
            catch (FormatException)
            {
                // القيمة مش Base64 — غالبًا plaintext قديم قبل الـ encryption
                // نرجعه زي ما هو بدل ما نكسر الـ app
                return cipherText;
            }

            if (data.Length < NonceSize + TagSize)
                return cipherText; // بيانات قصيرة أوي — نفس المعالجة

            var nonce = data[..NonceSize];
            var tag = data[NonceSize..(NonceSize + TagSize)];
            var cipher = data[(NonceSize + TagSize)..];
            var plainBytes = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);

            try
            {
                aes.Decrypt(nonce, cipher, tag, plainBytes);
            }
            catch (AuthenticationTagMismatchException)
            {
                // الـ tag مش صح — البيانات اتعدلت أو الـ key غلط
                throw new InvalidOperationException(
                    "Decryption failed: authentication tag mismatch. " +
                    "Verify the encryption key is correct and data was not tampered with.");
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}