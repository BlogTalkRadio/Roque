using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Redis;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Trugger that executes every N seconds
    /// </summary>
    public class IntervalTrigger : Trigger
    {
        protected Func<DateTime?, DateTime?> NextExecutionGetter;

        protected override DateTime? GetNextExecution(DateTime? lastExecution)
        {
            if (NextExecutionGetter == null)
            {
                var interval = Settings.Get("intervalSeconds", 30);
                if (interval <= 0)
                {
                    throw new Exception("Interval must be bigger than zero");
                }
                NextExecutionGetter = (lastExec) => (lastExec ?? DateTime.UtcNow).AddSeconds(interval);
            }

            return NextExecutionGetter(lastExecution);
        }
    }
}
