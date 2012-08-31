// -----------------------------------------------------------------------
// <copyright file="ShouldRetryException.cs" company="">
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
    /// If a job throws this exception the Worker will retry.
    /// </summary>
    public class ShouldRetryException : Exception
    {
        /// <summary>
        /// A delay to wait for next retry
        /// </summary>
        public TimeSpan Delay { get; private set; }

        /// <summary>
        /// Maximum number of times to retry.
        /// </summary>
        public int MaxTimes { get; private set; }

        /// <summary>
        /// Creates a ShouldRetryException
        /// </summary>
        /// <param name="delay">A delay to wait for next retry</param>
        /// <param name="maxTimes">Maximum number of times to retry.</param>
        /// <param name="innerException">the error that caused the job to fail</param>
        public ShouldRetryException(TimeSpan delay, int maxTimes, Exception innerException)
            : base("The Job execution failed and should be retried: " + innerException.Message, innerException)
        {
            Delay = delay;
            MaxTimes = maxTimes;
        }
    }
}
