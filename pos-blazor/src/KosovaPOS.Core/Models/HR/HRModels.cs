using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.HR
{
    /// <summary>
    /// Leave request model for annual, sick, and special leave.
    /// Per Kosovo Labor Law Art. 55–68.
    /// </summary>
    [Table("HR_LeaveRequests")]
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [Required, StringLength(50)]
        public string LeaveType { get; set; } = "Vjetore";
        // Vjetore = Annual | Sëmundje = Sick | Lehonë = Maternity | Atësi = Paternity
        // Fatkeqësi = Bereavement | Martesë = Marriage | Papaguar = Unpaid

        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }

        [NotMapped]
        public int DaysCount => (int)(EndDate - StartDate).TotalDays + 1;

        [StringLength(500)]
        public string? Reason { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Në pritje";
        // Në pritje = Pending | Aprovuar = Approved | Refuzuar = Rejected

        public DateTime RequestDate { get; set; } = DateTime.Now;

        [StringLength(200)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovalDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Monthly payroll record with Kosovo-specific tax calculations.
    /// Used to generate payroll sheets and ATK declarations (TP-1, AKK-1).
    /// </summary>
    [Table("HR_PayrollRecords")]
    public class PayrollRecord
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        public int Year  { get; set; }
        public int Month { get; set; }

        [NotMapped]
        public string Period => $"{Month:D2}/{Year}";

        public decimal GrossSalary       { get; set; }
        public decimal Bonus             { get; set; }
        public decimal OvertimePay       { get; set; }
        public decimal OtherAllowances   { get; set; }

        [NotMapped]
        public decimal TotalGross => GrossSalary + Bonus + OvertimePay + OtherAllowances;

        // ── Deductions ────────────────────────────────────────────────────────────────

        /// <summary>Employee pension contribution – 5% of gross (KSPF).</summary>
        public decimal EmployeePension   { get; set; }

        /// <summary>Personal income tax (TAP) per Kosovo brackets.</summary>
        public decimal IncomeTax         { get; set; }

        public decimal OtherDeductions   { get; set; }

        [NotMapped]
        public decimal TotalDeductions => EmployeePension + IncomeTax + OtherDeductions;

        [NotMapped]
        public decimal NetPay => TotalGross - TotalDeductions;

        // ── Employer costs ────────────────────────────────────────────────────────────

        /// <summary>Employer pension contribution – 5% of gross.</summary>
        public decimal EmployerPension   { get; set; }

        [NotMapped]
        public decimal TotalEmployerCost => TotalGross + EmployerPension;

        // ── Payment info ──────────────────────────────────────────────────────────────

        public bool    IsPaid       { get; set; } = false;
        public DateTime? PaidDate   { get; set; }

        [StringLength(500)]
        public string? Notes        { get; set; }

        public DateTime CreatedAt   { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Certificate issued to an employee (employment, salary, experience).
    /// </summary>
    [Table("HR_EmployeeCertificates")]
    public class EmployeeCertificate
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [Required, StringLength(50)]
        public string CertificateType { get; set; } = "Punësim";
        // Punësim = Employment | Pagë = Salary | Stazhit = Experience | Rekomandim = Reference

        [Required, StringLength(50)]
        public string CertificateNumber { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; } = DateTime.Today;

        [StringLength(500)]
        public string? Purpose { get; set; }    // Qëllimi (për vizë, për kredi, etj.)

        [StringLength(200)]
        public string? IssuedBy { get; set; }   // Lëshuar nga

        [StringLength(100)]
        public string? IssuedByTitle { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
