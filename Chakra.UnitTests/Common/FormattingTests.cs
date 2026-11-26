using Chakra.Application.Common;
using Chakra.Application.Features.Scheduler;
using Chakra.Domain.Entities;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Entities.Reminders;
using Chakra.UnitTests.Support;

namespace Chakra.UnitTests.CommonFormattingTests;

public class FormattingTests
{
    private const string Link = "https://pay.test/x";
    private static readonly ReminderPolicy Policy = new(3, 1, 3, 27);

    // 12.000.000 / 7: cicilan 1 = 1.714.285, jatuh tempo 15 Jan 2027, grace 7 hari
    private static Premi NewPremi() =>
        PremiFactory.Create(total: 12_000_000m, tenor: 7, grace: 7, start: new DateOnly(2027, 1, 10));

    [Fact]
    public void RupiahFormat_UsesDotAsThousandsSeparator()
    {
        Assert.Equal("1.714.285", RupiahFormat.Format(1_714_285m));
    }

    [Fact]
    public void ForCustomer_Upcoming_MatchesSpec()
    {
        var premi = NewPremi();
        var today = new DateOnly(2027, 1, 12);
        var decision = premi.EvaluateReminders(today, Policy);
        Assert.Equal(ReminderKind.Upcoming, decision.Kind);

        var text = ReminderMessages.ForCustomer(decision, Link, today);

        Assert.Equal(
            "Pengingat: cicilan ke-1 sebesar Rp1.714.285 jatuh tempo pada 15 Jan 2027 (3 hari lagi).\n" +
            "Bayar sekarang: https://pay.test/x", text);
    }

    [Fact]
    public void ForCustomer_DueTomorrow_MatchesSpec()
    {
        var premi = NewPremi();
        var today = new DateOnly(2027, 1, 14);
        var decision = premi.EvaluateReminders(today, Policy);

        var text = ReminderMessages.ForCustomer(decision, Link, today);

        Assert.Equal(
            "Cicilan ke-1 sebesar Rp1.714.285 jatuh tempo besok (15 Jan 2027).\n" +
            "Bayar sekarang: https://pay.test/x", text);
    }

    [Fact]
    public void ForCustomer_DueToday_MatchesSpec()
    {
        var premi = NewPremi();
        var today = new DateOnly(2027, 1, 15);
        var decision = premi.EvaluateReminders(today, Policy);

        var text = ReminderMessages.ForCustomer(decision, Link, today);

        Assert.Equal(
            "Cicilan ke-1 sebesar Rp1.714.285 jatuh tempo hari ini. Masa tenggang sampai 22 Jan 2027.\n" +
            "Bayar sekarang: https://pay.test/x", text);
    }

    [Fact]
    public void ForCustomer_GracePeriod_MatchesSpec()
    {
        var premi = NewPremi();
        var today = new DateOnly(2027, 1, 18);
        var decision = premi.EvaluateReminders(today, Policy);

        var text = ReminderMessages.ForCustomer(decision, Link, today);

        Assert.Equal(
            "Cicilan ke-1 sebesar Rp1.714.285 sudah lewat jatuh tempo. Masa tenggang berakhir 22 Jan 2027.\n" +
            "Bayar sekarang: https://pay.test/x", text);
    }

    [Fact]
    public void ForCustomer_Overdue_MatchesSpec()
    {
        var premi = NewPremi();
        var today = new DateOnly(2027, 1, 24);
        var decision = premi.EvaluateReminders(today, Policy);
        Assert.Equal(ReminderKind.Overdue, decision.Kind);

        var text = ReminderMessages.ForCustomer(decision, Link, today);

        Assert.Equal(
            "Cicilan ke-1 sebesar Rp1.714.285 terlambat 9 hari.\n" +
            "Segera bayar: https://pay.test/x", text);
    }

    [Fact]
    public void ForAdmin_MatchesSpec()
    {
        var premi = NewPremi();
        var user = new User { Name = "Budi", Email = "budi@example.com" };

        var text = ReminderMessages.ForAdmin(user, PremiFactory.Nth(premi, 1));

        Assert.Equal(
            "Tunggakan 30 hari.\nNasabah: Budi (budi@example.com)\n" +
            "Cicilan ke-1, Rp1.714.285, jatuh tempo 15 Jan 2027.\n" +
            "Pengingat tetap dikirim. Hentikan lewat menu premi jika perlu.", text);
    }
}
