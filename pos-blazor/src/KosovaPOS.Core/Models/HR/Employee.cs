using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.HR
{
    /// <summary>
    /// Employee model per Kosovo Labor Law (Ligji Nr. 03/L-212 për Marrëdhëniet e Punës).
    /// Stores all mandatory personnel data required by Kosovo ATK and Labour Inspectorate.
    /// </summary>
    [Table("HR_Employees")]
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        // ── Personal Information ──────────────────────────────────────────────────────

        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>Personal ID number (Numri Personal / CRNK) – mandatory for ATK.</summary>
        [StringLength(20)]
        public string? PersonalIdNumber { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; } // Mashkull, Femër

        [StringLength(100)]
        public string? Nationality { get; set; } = "Kosovar/e";

        [StringLength(300)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(150)]
        public string? Email { get; set; }

        // ── Employment Details ────────────────────────────────────────────────────────

        [Required, StringLength(200)]
        public string Position { get; set; } = string.Empty;       // Pozita

        [StringLength(100)]
        public string? Department { get; set; }                    // Departamenti / Sektori

        [StringLength(100)]
        public string? Branch { get; set; }                        // Filiala

        public DateTime HireDate { get; set; } = DateTime.Today;   // Data e fillimit

        public DateTime? TerminationDate { get; set; }             // Data e largimit

        [StringLength(50)]
        public string ContractType { get; set; } = "I pacaktuar";  // I pacaktuar / Me afat / Me kohë të pjesshme

        /// <summary>Contract end date – only relevant for fixed-term contracts.</summary>
        public DateTime? ContractEndDate { get; set; }

        [StringLength(100)]
        public string? ProbationPeriod { get; set; }               // Periudha e provës (e.g. "3 muaj")

        public int WeeklyHours { get; set; } = 40;                 // Orë/javë (standard = 40)

        // ── Salary ───────────────────────────────────────────────────────────────────

        /// <summary>Gross monthly salary in EUR.</summary>
        public decimal GrossSalary { get; set; }

        /// <summary>Net salary after all deductions (calculated).</summary>
        [NotMapped]
        public decimal NetSalary => KosovoPayrollCalculator.CalculateNet(GrossSalary);

        /// <summary>Employee pension contribution (5% of gross).</summary>
        [NotMapped]
        public decimal EmployeePension => KosovoPayrollCalculator.EmployeePension(GrossSalary);

        /// <summary>Employer pension contribution (5% of gross).</summary>
        [NotMapped]
        public decimal EmployerPension => KosovoPayrollCalculator.EmployerPension(GrossSalary);

        /// <summary>Personal Income Tax (Tatimi mbi të Ardhurat Personale).</summary>
        [NotMapped]
        public decimal IncomeTax => KosovoPayrollCalculator.IncomeTax(GrossSalary);

        [StringLength(50)]
        public string? BankAccount { get; set; }   // Xhirollogaria

        [StringLength(100)]
        public string? BankName { get; set; }

        // ── Leave Entitlement ─────────────────────────────────────────────────────────

        /// <summary>Annual leave days. Minimum 20 per Kosovo Labor Law Art. 59.</summary>
        public int AnnualLeaveDays { get; set; } = 20;

        public int UsedLeaveDays { get; set; } = 0;

        [NotMapped]
        public int RemainingLeaveDays => AnnualLeaveDays - UsedLeaveDays;

        // ── Employment Status ─────────────────────────────────────────────────────────

        public bool IsActive { get; set; } = true;

        [StringLength(50)]
        public string EmploymentStatus { get; set; } = "Aktiv";    // Aktiv, Suspenduar, Larguar

        [StringLength(500)]
        public string? TerminationReason { get; set; }

        // ── Documents ─────────────────────────────────────────────────────────────────

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ── Navigation ────────────────────────────────────────────────────────────────

        public List<EmploymentContract> Contracts { get; set; } = new();
        public List<LeaveRequest> LeaveRequests  { get; set; } = new();
        public List<PayrollRecord> PayrollRecords { get; set; } = new();
        public List<EmployeeCertificate> Certificates { get; set; } = new();
    }
}
