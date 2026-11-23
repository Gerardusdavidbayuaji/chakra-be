using System.Globalization;

namespace Chakra.Application.Common;

public static class RupiahFormat
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    // 1714285 -> "1.714.285"
    public static string Format(decimal amount) => amount.ToString("N0", Indonesian);
}
