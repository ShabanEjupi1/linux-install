using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models
{
    public enum TableStatus
    {
        Free      = 0,
        Occupied  = 1,
        Reserved  = 2,
        Cleaning  = 3
    }

    [Table("RestaurantTables")]
    public class RestaurantTable
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string TableNumber { get; set; } = "";

        public int Capacity { get; set; } = 4;

        [StringLength(100)]
        public string? Section { get; set; }

        public TableStatus Status { get; set; } = TableStatus.Free;

        public int? ActiveReceiptId { get; set; }

        public int? AssignedWaiterId { get; set; }

        public DateTime? OccupiedSince { get; set; }

        // Floor-plan positioning (pixels on canvas)
        [Column(TypeName = "decimal(10,2)")]
        public decimal PositionX { get; set; } = 50;

        [Column(TypeName = "decimal(10,2)")]
        public decimal PositionY { get; set; } = 50;

        public int Width  { get; set; } = 80;
        public int Height { get; set; } = 80;

        public bool IsRound { get; set; }

        public bool IsActive { get; set; } = true;
    }

    [Table("TableSections")]
    public class TableSection
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        [StringLength(7)]
        public string? ColorHex { get; set; } = "#3B82F6";

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
