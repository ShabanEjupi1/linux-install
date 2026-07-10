using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum AppointmentStatus
    {
        Scheduled  = 0,
        Confirmed  = 1,
        InProgress = 2,
        Completed  = 3,
        Cancelled  = 4,
        NoShow     = 5
    }

    [Table("Appointments")]
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        [StringLength(200)]
        public string? CustomerName { get; set; }

        [StringLength(50)]
        public string? CustomerPhone { get; set; }

        public int? EmployeeId { get; set; }

        [StringLength(200)]
        public string? EmployeeName { get; set; }

        /// <summary>Optional: the Article (service) associated with this appointment.</summary>
        public int? ServiceArticleId { get; set; }

        [StringLength(250)]
        public string? ServiceName { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ServicePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Deposit { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool ReminderSent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Set when appointment is converted to a receipt.</summary>
        public int? ReceiptId { get; set; }
    }

    [Table("EmployeeSchedules")]
    public class EmployeeSchedule
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [StringLength(200)]
        public string EmployeeName { get; set; } = "";

        /// <summary>0=Sunday … 6=Saturday</summary>
        public int DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsWorkingDay { get; set; } = true;
    }
}
