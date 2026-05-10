using System.Security.Cryptography;
using System.Text;

namespace PowerWordRelive.Infrastructure.Security;

public static class AesAuth
{
    public static string GenerateKey()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        return Convert.ToBase64String(keyBytes);
    }

    public static byte[] ParseKey(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new ArgumentException("Key must not be empty");
        return Convert.FromBase64String(base64Key);
    }

    public static string GenerateChallenge()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    }

    public static string EncryptChallenge(string challenge, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(challenge);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public static bool VerifyChallenge(string challenge, string encryptedResponse, byte[] key)
    {
        try
        {
            var data = Convert.FromBase64String(encryptedResponse);
            var iv = data[..16];
            var ciphertext = data[16..];

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            var decrypted = Encoding.UTF8.GetString(plainBytes);
            return decrypted == challenge;
        }
        catch
        {
            return false;
        }
    }
}