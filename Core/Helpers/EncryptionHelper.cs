using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PromptMasterv6.Core.Helpers
{
    public static class EncryptionHelper
    {
        private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes("PromptMaster_v5_Secret_Key"));

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = Key;
                    aes.GenerateIV();

                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(aes.IV, 0, aes.IV.Length);

                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                            cs.Write(plainBytes, 0, plainBytes.Length);
                        }

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                return plainText;
            }
        }

        public static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return encryptedText;

            try
            {
                Span<byte> buffer = new Span<byte>(new byte[encryptedText.Length]);
                if (!Convert.TryFromBase64String(encryptedText, buffer, out int bytesWritten))
                {
                    return encryptedText;
                }

                byte[] cipherBytes = Convert.FromBase64String(encryptedText);

                if (cipherBytes.Length < 16) return encryptedText;

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Key;

                    byte[] iv = new byte[16];
                    Array.Copy(cipherBytes, 0, iv, 0, 16);
                    aes.IV = iv;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 16, cipherBytes.Length - 16);
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch
            {
                return encryptedText;
            }
        }
    }
}
