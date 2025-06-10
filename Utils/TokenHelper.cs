using System;
using System.Text;

public static class TokenHelper
{
    public static string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainBytes);
    }

    public static string Decrypt(string encryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        return Encoding.UTF8.GetString(encryptedBytes);
    }
}