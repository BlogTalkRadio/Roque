namespace Cinchcast.Roque.Core
{
    using System;

    /// <summary>
    /// <see cref="System.Type"/> extension methods
    /// </summary>
    internal static class TypeExtensions
    {
        /// <summary>
        /// Determines whether the specified the type is nullable.
        /// </summary>
        /// <param name="theType">The type.</param>
        /// <returns>
        ///   <c>true</c> if the specified the type is nullable; otherwise, <c>false</c>.
        /// </returns>
        internal static bool IsNullable(this Type theType)
        {
            if (theType == null)
                throw new ArgumentNullException("theType", "The type cannot be null");

            return theType.IsGenericType && theType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
