using System.Globalization;
using Chakra.Application.Common;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Entities.Reminders;

namespace Chakra.Application.Features.Scheduler;

// Teks Telegram Bahasa Indonesia, persis tabel di spec scheduler bagian 3
public static class ReminderMessages
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    // today dibutuhkan untuk {sisa} pada Upcoming (decision tidak memuat tanggal hari ini)
    public static string ForCustomer(ReminderDecision decision, string paymentUrl, DateOnly today)
    {
        var installment = decision.Target
            ?? throw new InvalidOperationException("Reminder decision has no target installment.");
        var kind = decision.Kind
            ?? throw new InvalidOperationException("Reminder decision has no message kind.");

        var number = installment.InstallmentNumber;
        var amount = RupiahFormat.Format(installment.Amount);
        var due = FormatDate(installment.DueDate);
        var graceEnd = decision.GracePeriodEndsOn.HasValue ? FormatDate(decision.GracePeriodEndsOn.Value) : due;

        return kind switch
        {
            ReminderKind.Upcoming =>
                $"Pengingat: cicilan ke-{number} sebesar Rp{amount} jatuh tempo pada {due} " +
                $"({Math.Max(1, installment.DueDate.DayNumber - today.DayNumber)} hari lagi).\nBayar sekarang: {paymentUrl}",
            ReminderKind.DueTomorrow =>
                $"Cicilan ke-{number} sebesar Rp{amount} jatuh tempo besok ({due}).\nBayar sekarang: {paymentUrl}",
            ReminderKind.DueToday =>
                $"Cicilan ke-{number} sebesar Rp{amount} jatuh tempo hari ini. Masa tenggang sampai {graceEnd}.\nBayar sekarang: {paymentUrl}",
            ReminderKind.GracePeriod =>
                $"Cicilan ke-{number} sebesar Rp{amount} sudah lewat jatuh tempo. Masa tenggang berakhir {graceEnd}.\nBayar sekarang: {paymentUrl}",
            ReminderKind.Overdue =>
                $"Cicilan ke-{number} sebesar Rp{amount} terlambat {decision.DaysLate} hari.\nSegera bayar: {paymentUrl}",
            _ => throw new ArgumentOutOfRangeException(nameof(decision), kind, "Unknown reminder kind.")
        };
    }

    public static string ForAdmin(User user, Installment installment) =>
        $"Tunggakan 30 hari.\nNasabah: {user.Name} ({user.Email})\n" +
        $"Cicilan ke-{installment.InstallmentNumber}, Rp{RupiahFormat.Format(installment.Amount)}, " +
        $"jatuh tempo {FormatDate(installment.DueDate)}.\n" +
        "Pengingat tetap dikirim. Hentikan lewat menu premi jika perlu.";

    private static string FormatDate(DateOnly date) => date.ToString("dd MMM yyyy", Indonesian);
}
