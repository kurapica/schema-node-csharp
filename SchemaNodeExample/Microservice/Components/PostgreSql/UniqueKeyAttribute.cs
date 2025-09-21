using System;
using System.ComponentModel.DataAnnotations;

namespace SchemaNode.Example
{
    /// <summary>
    /// Marks an EntityFramework Entity class property to be a Unique Key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class UniqueKeyAttribute : ValidationAttribute
    {
        #region Constructors

        /// <summary>
        /// Marker attribute for unique key
        /// </summary>
        /// <param name="groupId">Optional, used to group multiple entity properties together into a combined Unique Key</param>
        /// <param name="order">Optional, used to order the entity properties that are part of a combined Unique Key</param>
        /// <param name="primary">Optional, used to set key to be primary</param>
        public UniqueKeyAttribute(string groupId = null, int order = 0, bool primary = false)
        {
            Group = groupId;
            Order = order;
            Primary = primary;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Group multiple entity properties together into a combined Unique Key.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Order the entity properties that are part of a combined Unique Key.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Whether this is a primary key
        /// </summary>
        public bool Primary { get; }

        #endregion

        #region Implementation

        /// <inheritdoc />
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            return ValidationResult.Success;
        }

        #endregion
    }
}
