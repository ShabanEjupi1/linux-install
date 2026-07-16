using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a staff member/employee
    /// </summary>
    public class StaffMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string Position { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        public decimal HourlyRate { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime HireDate { get; set; } = DateTime.Now;

        public DateTime? TerminationDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents employee work shifts
    /// </summary>
    public class WorkShift
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to employee (POSUser or Punetoret)
        /// </summary>
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        /// <summary>
        /// Shift type: Morning, Afternoon, Evening, Night, Custom
        /// </summary>
        [StringLength(20)]
        public string ShiftType { get; set; } = "Morning";

        public DateTime ShiftDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Actual clock-in time
        /// </summary>
        public DateTime? ClockInTime { get; set; }

        /// <summary>
        /// Actual clock-out time
        /// </summary>
        public DateTime? ClockOutTime { get; set; }

        /// <summary>
        /// Break duration in minutes
        /// </summary>
        public int BreakMinutes { get; set; } = 0;

        /// <summary>
        /// Shift status: Scheduled, InProgress, Completed, Absent, Late
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Scheduled";

        /// <summary>
        /// Position/Role: Cashier, Kitchen, Waiter, Driver, Manager
        /// </summary>
        [StringLength(50)]
        public string? Position { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Hourly rate for this shift
        /// </summary>
        public decimal HourlyRate { get; set; } = 0;

        /// <summary>
        /// Total hours worked (calculated)
        /// </summary>
        public decimal HoursWorked { get; set; } = 0;

        /// <summary>
        /// Labor cost for this shift (calculated)
        /// </summary>
        public decimal LaborCost { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Employee performance metrics
    /// </summary>
    public class EmployeePerformance
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        /// <summary>
        /// Performance period start
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Performance period end
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Total shifts worked
        /// </summary>
        public int ShiftsWorked { get; set; } = 0;

        /// <summary>
        /// Total hours worked
        /// </summary>
        public decimal TotalHours { get; set; } = 0;

        /// <summary>
        /// Number of times late
        /// </summary>
        public int LateCount { get; set; } = 0;

        /// <summary>
        /// Number of absences
        /// </summary>
        public int AbsenceCount { get; set; } = 0;

        /// <summary>
        /// Total sales handled (for cashiers/waiters)
        /// </summary>
        public decimal TotalSales { get; set; } = 0;

        /// <summary>
        /// Number of orders/transactions
        /// </summary>
        public int OrderCount { get; set; } = 0;

        /// <summary>
        /// Average order value
        /// </summary>
        public decimal AverageOrderValue { get; set; } = 0;

        /// <summary>
        /// Customer satisfaction rating (1-5)
        /// </summary>
        public decimal? CustomerRating { get; set; }

        /// <summary>
        /// Deliveries completed (for drivers)
        /// </summary>
        public int? DeliveriesCompleted { get; set; }

        /// <summary>
        /// Average delivery time in minutes
        /// </summary>
        public decimal? AverageDeliveryTime { get; set; }

        /// <summary>
        /// Performance score (calculated, 0-100)
        /// </summary>
        public decimal PerformanceScore { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Time tracking for clock in/out
    /// </summary>
    public class TimeCard
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        /// <summary>
        /// Date of the time card entry (added via migration if DB was old schema)
        /// </summary>
        public DateTime Date { get; set; } = DateTime.Now.Date;

        /// <summary>
        /// Maps to ClockIn column in DB (original schema used ClockIn)
        /// </summary>
        [Column("ClockInTime")]
        public DateTime ClockInTime { get; set; }

        /// <summary>
        /// Maps to ClockOut column in DB
        /// </summary>
        [Column("ClockOutTime")]
        public DateTime? ClockOutTime { get; set; }

        public DateTime? BreakStartTime { get; set; }
        public DateTime? BreakEndTime { get; set; }

        public int TotalBreakMinutes { get; set; } = 0;

        public decimal TotalHours { get; set; } = 0;

        public int? ShiftId { get; set; }

        [NotMapped]
        public string? StaffName => EmployeeName;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual WorkShift? Shift { get; set; }
    }

    /// <summary>
    /// Labor cost analysis
    /// </summary>
    public class LaborCostAnalysis
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Analysis period
        /// </summary>
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Total labor cost
        /// </summary>
        public decimal TotalLaborCost { get; set; }

        /// <summary>
        /// Total revenue for period
        /// </summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>
        /// Labor cost percentage
        /// </summary>
        public decimal LaborCostPercentage { get; set; }

        /// <summary>
        /// Total hours worked by all employees
        /// </summary>
        public decimal TotalHours { get; set; }

        /// <summary>
        /// Average hourly cost
        /// </summary>
        public decimal AverageHourlyCost { get; set; }

        /// <summary>
        /// Number of employees
        /// </summary>
        public int EmployeeCount { get; set; }

        /// <summary>
        /// Breakdown by position (JSON)
        /// </summary>
        public string? CostByPosition { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Employee availability for scheduling
    /// </summary>
    public class EmployeeAvailability
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        /// <summary>
        /// Day of week: Monday, Tuesday, etc.
        /// </summary>
        [StringLength(20)]
        public string DayOfWeek { get; set; } = "Monday";

        /// <summary>
        /// Available start time
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Available end time
        /// </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Is available on this day/time?
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Preferred shift type
        /// </summary>
        [StringLength(20)]
        public string? PreferredShift { get; set; }

        /// <summary>
        /// Maximum hours per week
        /// </summary>
        public decimal? MaxHoursPerWeek { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Time-off requests
    /// </summary>
    public class TimeOffRequest
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        /// <summary>
        /// Type: Vacation, Sick, Personal, Emergency
        /// </summary>
        [StringLength(20)]
        public string RequestType { get; set; } = "Vacation";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [StringLength(1000)]
        public string? Reason { get; set; }

        /// <summary>
        /// Status: Pending, Approved, Rejected
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Approved by (manager)
        /// </summary>
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)]
        public string? ApprovalNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
