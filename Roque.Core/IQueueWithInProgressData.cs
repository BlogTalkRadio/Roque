// -----------------------------------------------------------------------
// <copyright file="IQueueWithInProgress.cs" company="">
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
    /// A Queue that keeps tracking of in progress jobs for each worker. Provides support for fail recovery.
    /// </summary>
    public interface IQueueWithInProgressData
    {
        string GetInProgressJson(Worker worker);

        void JobCompleted(Worker worker, Job job, bool failed);
    }
}
