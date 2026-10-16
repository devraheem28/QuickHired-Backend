using System;
using System.ComponentModel.DataAnnotations;

namespace QuickHired.BOL
{
    /// <summary>
    /// Represents the base entity class that provides a unique identifier for all entities.
    /// </summary>
    public class BaseEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for an entity.
        /// </summary>
        [Key]
        public int Id { get; set; }
    }
}
