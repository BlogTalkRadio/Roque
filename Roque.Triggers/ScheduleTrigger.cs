using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Redis;

namespace Cinchcast.Roque.Triggers
{
    public class ScheduleTrigger : Trigger
    {
        protected Func<DateTime?, DateTime?> MextExecutionGetter;

        protected override DateTime? GetNextExecution(DateTime? lastExecution)
        {
            if (MextExecutionGetter == null)
            {
                var schedule = Schedule.Create(Settings.Get<string, string, string>("schedule"));
                MextExecutionGetter = schedule.GetNextExecution;
            }

            return MextExecutionGetter(lastExecution);
        }
    }
}
