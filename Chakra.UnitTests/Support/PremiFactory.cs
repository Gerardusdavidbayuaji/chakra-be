using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Common;

namespace Chakra.UnitTests.Support;

internal static class PremiFactory
{
    public static readonly DateTime Now = new(2027, 1, 10, 2, 0, 0, DateTimeKind.Utc);

    // Default: 3 cicilan x 4.000.000, jatuh tempo 15 Jan / 15 Feb / 15 Mar 2027, grace 7 hari
    public static Premi Create(decimal total = 12_000_000m, int tenor = 3, int dueDay = 15,
        int grace = 7, DateOnly? start = null) =>
        Premi.Create(UserId.New(), total, tenor, dueDay, grace, start ?? new DateOnly(2027, 1, 10));

    public static void PayAll(Premi premi)
    {
        foreach (var installment in premi.Installments.OrderBy(i => i.InstallmentNumber).ToList())
            premi.MarkInstallmentPaid(installment.Id, Now);
    }

    public static Installment Nth(Premi premi, int number) =>
        premi.Installments.Single(i => i.InstallmentNumber == number);
}
