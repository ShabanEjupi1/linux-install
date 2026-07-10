using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.HR
{
    /// <summary>
    /// Employment contract model per Kosovo Labor Law (Ligji Nr. 03/L-212).
    /// Art. 6 defines mandatory contract elements.
    /// </summary>
    [Table("HR_EmploymentContracts")]
    public class EmploymentContract
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [Required, StringLength(50)]
        public string ContractNumber { get; set; } = string.Empty;

        // ── Contract Terms ────────────────────────────────────────────────────────────

        [Required, StringLength(50)]
        public string ContractType { get; set; } = "I pacaktuar";
        // I pacaktuar = Indefinite | Me afat = Fixed-term | Me kohë të pjesshme = Part-time

        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Null for indefinite contracts.</summary>
        public DateTime? EndDate { get; set; }

        [Required, StringLength(200)]
        public string Position { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Duties { get; set; }     // Detyrat dhe përgjegjësitë

        [StringLength(200)]
        public string? WorkPlace { get; set; }  // Vendi i punës

        public int WeeklyHours { get; set; } = 40;

        // ── Probation (Art. 12) ───────────────────────────────────────────────────────

        public bool HasProbation { get; set; } = false;

        public int ProbationMonths { get; set; } = 3;

        // ── Compensation ─────────────────────────────────────────────────────────────

        public decimal GrossSalary { get; set; }

        [StringLength(100)]
        public string? BonusDescription { get; set; }

        [StringLength(200)]
        public string? Benefits { get; set; }   // Benefitet (ushqimi, transporti, etj.)

        // ── Leave (Art. 59) ───────────────────────────────────────────────────────────

        public int AnnualLeaveDays { get; set; } = 20;

        // ── Notice Period (Art. 75+) ──────────────────────────────────────────────────

        [StringLength(50)]
        public string NoticePeriod { get; set; } = "30 ditë";

        // ── Metadata ─────────────────────────────────────────────────────────────────

        [StringLength(50)]
        public string Status { get; set; } = "Aktive"; // Aktive, Skaduar, Anuluar

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        // ── Employer signatory info ───────────────────────────────────────────────────

        [StringLength(200)]
        public string? EmployerRepresentative { get; set; }

        [StringLength(100)]
        public string? EmployerRepresentativeTitle { get; set; }
    }
}
