using Chakra.Domain.Entities.Common;
using Chakra.Domain.Entities.Enums;
using Chakra.Domain.Entities.Reminders;
using Chakra.Domain.Events;
using Chakra.Domain.Exceptions;

namespace Chakra.Domain.Entities;

// Aggregate root: semua perubahan cicilan lewat method di sini.
// Id, CreatedAt (waktu premi dibuat), UpdatedAt (waktu terakhir premi diupdate) ada di base class
public class Premi : Aggregate<PremiId>
{
    private readonly List<Installment> _installments = new();

    public UserId UserId { get; private set; } // menunjukan premi ini untuk siapa
    public decimal TotalAmount { get; private set; } // total keseluruhan pembayaran premi dalam rupiah bulat (misal: Rp 12.000.000)
    public decimal InstallmentAmount { get; private set; } // nominal dasar per cicilan = floor(TotalAmount / Tenor); cicilan terakhir menampung sisanya
    public int Tenor { get; private set; } // jumlah bulan cicilan (misal: 12 bulan)
    public int DueDay { get; private set; } // tanggal jatuh tempo setiap bulan (1–31). Misal 15 = setiap tanggal 15
    public int GracePeriodDays { get; private set; } // masa tenggang setelah jatuh tempo sebelum dianggap overdue (misal: 7 hari)
    public DateOnly StartDate { get; private set; } // tanggal mulai premi, cicilan pertama jatuh tempo di bulan ini (atau bulan berikutnya jika DueDay sudah lewat)
    public PremiStatus Status { get; private set; } // Status premi: Active (berjalan), Completed (lunas semua), Cancelled (dibatalkan)

    public bool RemindersPaused { get; private set; } // true: tidak ada pesan/alert, perubahan status tetap jalan
    public DateTime? RemindersPausedAt { get; private set; } // UTC
    public UserId? RemindersPausedByUserId { get; private set; } // admin yang menghentikan

    public User User { get; private set; } = null!;
    public IReadOnlyCollection<Installment> Installments => _installments.AsReadOnly();

    // Ringkasan, tidak disimpan ke database. Cicilan "aktif" = bukan Paid dan bukan Cancelled.
    public int InstallmentsPaid => _installments.Count(i => i.Status == InstallmentStatus.Paid);
    public decimal RemainingAmount => ActiveInstallments().Sum(i => i.Amount);
    public DateOnly? NextDueDate => ActiveInstallments().OrderBy(i => i.DueDate).FirstOrDefault()?.DueDate;

    private Premi()
    {
    }

    public static Premi Create(UserId userId, decimal totalAmount, int tenor, int dueDay,
        int gracePeriodDays, DateOnly startDate)
    {
        var premi = new Premi
        {
            Id = PremiId.New(),
            UserId = userId,
            Status = PremiStatus.Active
        };

        premi.ApplyTerms(totalAmount, tenor, dueDay, gracePeriodDays, startDate);
        return premi;
    }

    public void Update(decimal totalAmount, int tenor, int dueDay, int gracePeriodDays, DateOnly startDate)
    {
        if (Status != PremiStatus.Active)
            throw new DomainException("Only active premi can be changed.");

        if (_installments.Any(i => i.Status == InstallmentStatus.Paid))
            throw new DomainException("Premi cannot be changed because installments have already been paid.");

        ApplyTerms(totalAmount, tenor, dueDay, gracePeriodDays, startDate);
    }

    public void Cancel()
    {
        if (Status == PremiStatus.Cancelled)
            throw new DomainException("Premi is already cancelled.");

        if (Status == PremiStatus.Completed)
            throw new DomainException("Completed premi cannot be cancelled.");

        Status = PremiStatus.Cancelled;

        foreach (var installment in _installments.Where(i => i.Status != InstallmentStatus.Paid))
            installment.Cancel();

        AddDomainEvent(new PremiCancelledEvent(Id));
    }

    // Dipakai pembayaran manual admin sekarang dan webhook Midtrans nanti
    public Installment MarkInstallmentPaid(InstallmentId installmentId, DateTime paidAt, string? midtransOrderId = null)
    {
        var installment = _installments.FirstOrDefault(i => i.Id == installmentId);

        // Notifikasi ulang untuk order yang sama: sukses tanpa perubahan
        if (installment is { Status: InstallmentStatus.Paid }
            && midtransOrderId != null
            && midtransOrderId == installment.MidtransOrderId)
            return installment;

        var rejection = CheckCanPay(installmentId);
        if (rejection != null)
            throw new DomainException(rejection.Value switch
            {
                PaymentRejectionReason.PremiNotActive => "Premi is not active.",
                PaymentRejectionReason.InstallmentNotFound => "Installment not found in this premi.",
                PaymentRejectionReason.InstallmentAlreadyPaid => "Installment is already paid.",
                PaymentRejectionReason.PreviousInstallmentUnpaid => "Previous installments must be paid first.",
                _ => "Installment cannot be paid."
            });

        installment!.MarkPaid(paidAt, midtransOrderId);

        if (_installments.All(i => i.Status == InstallmentStatus.Paid))
            Status = PremiStatus.Completed;

        AddDomainEvent(new InstallmentPaidEvent(
            installment.Id, Id, UserId, installment.InstallmentNumber, installment.Amount,
            Status == PremiStatus.Completed, midtransOrderId));

        return installment;
    }

    // Alasan pertama yang berlaku, atau null jika cicilan boleh dibayar
    public PaymentRejectionReason? CheckCanPay(InstallmentId installmentId)
    {
        if (Status != PremiStatus.Active)
            return PaymentRejectionReason.PremiNotActive;

        var installment = _installments.FirstOrDefault(i => i.Id == installmentId);
        if (installment == null)
            return PaymentRejectionReason.InstallmentNotFound;

        if (installment.Status == InstallmentStatus.Paid)
            return PaymentRejectionReason.InstallmentAlreadyPaid;

        if (_installments.Any(i => i.InstallmentNumber < installment.InstallmentNumber
                                   && i.Status != InstallmentStatus.Paid))
            return PaymentRejectionReason.PreviousInstallmentUnpaid;

        return null;
    }

    public Installment? NextUnpaidInstallment() =>
        ActiveInstallments().OrderBy(i => i.InstallmentNumber).FirstOrDefault();

    public void PauseReminders(UserId adminId, DateTime now)
    {
        if (Status != PremiStatus.Active)
            throw new DomainException("Only active premi can pause reminders.");

        if (RemindersPaused)
            throw new DomainException("Reminders are already paused.");

        RemindersPaused = true;
        RemindersPausedAt = now;
        RemindersPausedByUserId = adminId;
    }

    public void ResumeReminders()
    {
        if (!RemindersPaused)
            throw new DomainException("Reminders are not paused.");

        RemindersPaused = false;
        RemindersPausedAt = null;
        RemindersPausedByUserId = null;
    }

    // Menerapkan perubahan status untuk hari ini dan mengembalikan keputusan pesan/alert
    public ReminderDecision EvaluateReminders(DateOnly today, ReminderPolicy policy)
    {
        if (Status != PremiStatus.Active)
            return ReminderDecision.None;

        // Cicilan belum lunas (termasuk target) yang lewat masa tenggang -> Overdue
        foreach (var installment in ActiveInstallments().ToList())
        {
            if (today > installment.DueDate.AddDays(GracePeriodDays))
                installment.PromoteTo(InstallmentStatus.Overdue);
        }

        var target = NextUnpaidInstallment();
        if (target == null)
            return ReminderDecision.None;

        var dueDate = target.DueDate;

        if (today >= dueDate.AddDays(-policy.DaysBeforeFirst))
            target.PromoteTo(InstallmentStatus.Reminder1);

        if (today >= dueDate.AddDays(-policy.DaysBeforeSecond))
            target.PromoteTo(InstallmentStatus.Reminder2);

        var graceEndsOn = dueDate.AddDays(GracePeriodDays);
        var daysLate = Math.Max(0, today.DayNumber - dueDate.DayNumber);

        if (RemindersPaused)
            return new ReminderDecision(target, null, daysLate, graceEndsOn, false);

        var adminAlertDue = today >= dueDate.AddDays(policy.AdminAlertAfterDueDays)
                            && target.OverdueAlertSentOn == null;

        var slot = LatestSlot(dueDate, today, policy);
        ReminderKind? kind = slot != null && (target.LastRemindedOn == null || target.LastRemindedOn < slot)
            ? KindFor(today, dueDate, GracePeriodDays, policy)
            : null;

        return new ReminderDecision(target, kind, daysLate, graceEndsOn, adminAlertDue);
    }

    // Dipanggil setelah pesan pengingat berhasil diantrekan
    public void RecordReminderSent(InstallmentId installmentId, DateOnly today) =>
        FindInstallment(installmentId).RecordReminder(today);

    // Dipanggil setelah alert admin berhasil dibuat
    public void RecordOverdueAlertSent(InstallmentId installmentId, DateOnly today) =>
        FindInstallment(installmentId).RecordOverdueAlert(today);

    private Installment FindInstallment(InstallmentId installmentId) =>
        _installments.FirstOrDefault(i => i.Id == installmentId)
        ?? throw new DomainException("Installment not found in this premi.");

    // Slot: D-first, D-second, D, D+k*repeat (k >= 1). Mengembalikan slot terbesar <= today, atau null
    private static DateOnly? LatestSlot(DateOnly dueDate, DateOnly today, ReminderPolicy policy)
    {
        if (today < dueDate.AddDays(-policy.DaysBeforeFirst))
            return null;

        if (today < dueDate.AddDays(-policy.DaysBeforeSecond))
            return dueDate.AddDays(-policy.DaysBeforeFirst);

        if (today < dueDate)
            return dueDate.AddDays(-policy.DaysBeforeSecond);

        if (policy.RepeatEveryDays <= 0)
            return dueDate;

        var cycles = (today.DayNumber - dueDate.DayNumber) / policy.RepeatEveryDays;
        return dueDate.AddDays(cycles * policy.RepeatEveryDays);
    }

    private static ReminderKind KindFor(DateOnly today, DateOnly dueDate, int gracePeriodDays, ReminderPolicy policy)
    {
        if (today < dueDate.AddDays(-policy.DaysBeforeSecond))
            return ReminderKind.Upcoming;

        if (today < dueDate)
            return ReminderKind.DueTomorrow;

        if (today == dueDate)
            return ReminderKind.DueToday;

        if (today <= dueDate.AddDays(gracePeriodDays))
            return ReminderKind.GracePeriod;

        return ReminderKind.Overdue;
    }

    private void ApplyTerms(decimal totalAmount, int tenor, int dueDay, int gracePeriodDays, DateOnly startDate)
    {
        if (totalAmount % 1 != 0)
            throw new DomainException("Total amount must be a whole rupiah amount.");

        if (totalAmount < tenor)
            throw new DomainException("Total amount must be at least equal to tenor.");

        TotalAmount = totalAmount;
        Tenor = tenor;
        DueDay = dueDay;
        GracePeriodDays = gracePeriodDays;
        StartDate = startDate;
        InstallmentAmount = Math.Floor(totalAmount / tenor);

        GenerateSchedule();
    }

    private void GenerateSchedule()
    {
        _installments.Clear();

        var firstDue = DueDateIn(StartDate.Year, StartDate.Month);
        var baseMonth = firstDue < StartDate ? StartDate.AddMonths(1) : StartDate; // hanya Year/Month yang dipakai

        for (var i = 1; i <= Tenor; i++)
        {
            // Dihitung dari bulan dasar setiap kali agar DueDay 31 tidak menyusut setelah Februari
            var month = new DateOnly(baseMonth.Year, baseMonth.Month, 1).AddMonths(i - 1);
            var amount = i == Tenor ? TotalAmount - InstallmentAmount * (Tenor - 1) : InstallmentAmount;
            _installments.Add(Installment.Create(Id, i, DueDateIn(month.Year, month.Month), amount));
        }
    }

    private DateOnly DueDateIn(int year, int month) =>
        new(year, month, Math.Min(DueDay, DateTime.DaysInMonth(year, month)));

    private IEnumerable<Installment> ActiveInstallments() =>
        _installments.Where(i => i.Status is not (InstallmentStatus.Paid or InstallmentStatus.Cancelled));
}
