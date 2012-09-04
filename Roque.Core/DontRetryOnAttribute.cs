// -----------------------------------------------------------------------
// <copyright file="RetryJobAttribute.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Specifies that when a certain type of Exception is raised, Worker must not retry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class DontRetryOnAttribute : RetryOnAttribute
    {
        /// <summary>
        /// An exception type on wich the job execution should NOT be retried
        /// </summary>
        public new Type ExceptionType { get; set; }

        public DontRetryOnAttribute(Type exceptionType)
            : base(exceptionType)
        {
            ExceptionType = exceptionType;
        }
    }
}
