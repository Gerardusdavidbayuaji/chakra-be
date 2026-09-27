using Chakra.Domain.Entities.Enums;

namespace Chakra.Domain.Entities;

public class Premi
{
    public Guid Id { get; set; } // identifier untuk setiap premi
    public Guid UserId { get; set; } // menunjukan premi ini untuk siapa
    public decimal TotalAmount { get; set; } // total keseluruhan pembayaran premi (misal: Rp 12.000.000)
    public decimal InstallmentAmount { get; set; } // jumlah per cicilan, dihitung otomatis TotalAmout / Tenor
    public int Tenor { get; set; } // jumlah bulan cicilan (misal: 12 bulan)
    public int DueDay { get; set; } // tanggal jatuh tempo setiap bulan (1–31). Misal 15 = setiap tanggal 15
    public int GracePeriodDays { get; set; } // masa tenggang setelah jatuh tempo sebelum dianggap overdue (misal: 7 hari)
    public DateOnly StartDate { get; set; } // tanggal mulai premi, cicilan pertama dihitung dari sini
    public PremiStatus Status { get; set; } // Status premi: Active (berjalan), Complated (lunas semua), Cancelled (dibatalkan)
    public DateTime CreatedAt { get; set; } // Waktu premi dibuat
    public DateTime UpdatedAt { get; set; } // Waktu terakhir premi diupdate
    
    public User User { get; set; }
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}