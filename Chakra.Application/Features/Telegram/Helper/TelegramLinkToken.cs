using System.Security.Cryptography;
using System.Text;

namespace Chakra.Application.Features.Telegram.Helper;

public static class TelegramLinkToken
{
    // 24 byte → 48 karakter hex, sesuai batas parameter start Telegram (maks 64, [A-Za-z0-9_-])
    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
