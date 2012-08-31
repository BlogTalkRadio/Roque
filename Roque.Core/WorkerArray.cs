// -----------------------------------------------------------------------
// <copyright file="WorkerSet.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// An array of <see cref="Worker"/>
    /// </summary>
    public class WorkerArray : IEnumerable<Worker>
    {

        private Worker[] _Workers;

        public WorkerArray(params Worker[] workers)
        {
            _Workers = workers;
        }

        /// <summary>
        /// Starts all workers
        /// </summary>
        public void Start()
        {
            ForEach(worker => worker.Start());
        }

        /// <summary>
        /// Request stop of all workers
        /// </summary>
        /// <returns></returns>
        public Task Stop()
        {
            var tasks = this.Select(worker => worker.Stop()).ToArray();
            return Task.Factory.StartNew(() =>
            {
                Task.WaitAll(tasks);
            });
        }

        public void ForEach(Action<Worker> action)
        {
            foreach (var worker in _Workers)
            {
                action(worker);
            }
        }

        public IEnumerator<Worker> GetEnumerator()
        {
            return _Workers.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _Workers.GetEnumerator();
        }
    }
}
