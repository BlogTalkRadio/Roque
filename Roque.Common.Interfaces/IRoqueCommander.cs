// -----------------------------------------------------------------------
// <copyright file="IRoqueCommander.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Cinchcast.Roque.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Work service interface example
    /// </summary>
    public interface IRoqueCommander
    {
        void StopWorker(string name);
        void StartWorker(string name);
    }
}
