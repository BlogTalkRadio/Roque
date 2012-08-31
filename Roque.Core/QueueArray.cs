// -----------------------------------------------------------------------
// <copyright file="QueueSet.cs" company="">
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
    /// An array of <see cref="Queue"/>
    /// </summary>
    public class QueueArray : IEnumerable<Queue>
    {

        private Queue[] _Queues;

        public QueueArray(params Queue[] workers)
        {
            _Queues = workers;
        }

        public void ForEach(Action<Queue> action)
        {
            foreach (var worker in _Queues)
            {
                action(worker);
            }
        }

        public IEnumerator<Queue> GetEnumerator()
        {
            return _Queues.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _Queues.GetEnumerator();
        }
    }
}
