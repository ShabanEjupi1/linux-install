using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Represents a topping that can be added to pizzas
    /// </summary>
    public class PizzaTopping
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        /// <summary>
        /// Additional price for this topping
        /// </summary>
        [Required]
        public decimal Price { get; set; }
        
        /// <summary>
        /// Category: Meat, Vegetable, Cheese, Sauce, etc.
        /// </summary>
        [StringLength(50)]
        public string? Category { get; set; }
        
        /// <summary>
        /// Is this topping currently available?
        /// </summary>
        public bool IsAvailable { get; set; } = true;
        
        /// <summary>
        /// Icon/emoji for display
        /// </summary>
        [StringLength(10)]
        public string? Icon { get; set; }
        
        /// <summary>
        /// Display order in UI
        /// </summary>
        public int DisplayOrder { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Junction table for pizza customization orders
    /// </summary>
    public class PizzaOrderTopping
    {
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// Reference to ReceiptItem
        /// </summary>
        public int ReceiptItemId { get; set; }
        
        /// <summary>
        /// Reference to PizzaTopping
        /// </summary>
        public int ToppingId { get; set; }
        
        /// <summary>
        /// Quantity of this topping (default 1, can be 0.5 for half, 2 for double)
        /// </summary>
        public decimal Quantity { get; set; } = 1;
        
        /// <summary>
        /// Price at time of order (for historical accuracy)
        /// </summary>
        public decimal Price { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Navigation properties
        public virtual PizzaTopping? Topping { get; set; }
    }
}
