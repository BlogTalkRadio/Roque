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
    /// Specifies that when a certain type of Exception is raised, Worker should retry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RetryOnAttribute : Attribute
    {
        /// <summary>
        /// An exception type on wich the job execution should be retried
        /// </summary>
        public Type ExceptionType { get; set; }

        /// <summary>
        /// Maximum number of times to retry
        /// </summary>
        public int MaxTimes { get; set; }

        /// <summary>
        /// Time to wait before retrying, by default retries are done immediately
        /// </summary>
        public int DelaySeconds { get; set; }

        public ShouldRetryException CreateException(Exception internalExcpetion)
        {
            return new ShouldRetryException(TimeSpan.FromSeconds(DelaySeconds), MaxTimes, internalExcpetion);
        }

        public RetryOnAttribute(Type exceptionType)
        {
            ExceptionType = exceptionType;
        }
    }
}
