using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Menu categories for pizzeria items
    /// </summary>
    public class PizzaCategory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        /// <summary>
        /// Icon/emoji for display
        /// </summary>
        [StringLength(10)]
        public string? Icon { get; set; }
        
        /// <summary>
        /// Display order in menu
        /// </summary>
        public int DisplayOrder { get; set; } = 0;
        
        /// <summary>
        /// Is this category currently active?
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
