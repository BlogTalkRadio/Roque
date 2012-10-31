using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cinchcast.Roque.Triggers
{
    public class TriggerWatcher
    {
        public Trigger[] Triggers { get; private set; }

        public bool IsWatching { get; private set; }

        private Task _CurrentWatch;

        public bool IsStopRequested { get; private set; }

        public TriggerWatcher(params Trigger[] triggers)
        {
            Triggers = triggers;
        }

        /// <summary>
        /// Starts watching triggers async
        /// </summary>
        /// <returns></returns>
        public Task Start()
        {
            return _CurrentWatch = Task.Factory.StartNew(Watch);
        }

        /// <summary>
        /// Request stop of this trigger watcher
        /// </summary>
        /// <returns></returns>
        public Task Stop()
        {
            if (_CurrentWatch == null || _CurrentWatch.IsCompleted)
            {
                return Task.Factory.StartNew(() => { });
            }
            if (!IsStopRequested)
            {
                IsStopRequested = true;
            }
            return _CurrentWatch;
        }

        private void Watch()
        {
            if (Triggers.Length < 1)
            {
                return;
            }

            bool firstTime = true;
            IsStopRequested = false;
            IsWatching = true;
            RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger watcher started. Triggers: {0}", string.Join(", ", Triggers.Select(t => t.Name)));
            while (!IsStopRequested)
            {
                int sleepMilliseconds = 900;
                try
                {
                    DateTime? nextExecution = null;
                    bool unknownExecutionTime = false;

                    // check each trigger next execution time
                    foreach (Trigger trigger in Triggers)
                    {
                        DateTime? triggerNextExecution = trigger.GetNextExecution();
                        if (triggerNextExecution != null)
                        {
                            if (triggerNextExecution <= DateTime.UtcNow)
                            {
                                trigger.Execute();
                            }
                            else if (!unknownExecutionTime)
                            {
                                if (nextExecution == null || nextExecution > triggerNextExecution)
                                {
                                    nextExecution = triggerNextExecution;
                                }
                            }
                        }
                        else
                        {
                            nextExecution = null;
                            unknownExecutionTime = true;
                        }
                    }

                    if (!unknownExecutionTime && nextExecution != null)
                    {
                        TimeSpan sleep = nextExecution.Value.Subtract(DateTime.UtcNow);
                        if (sleep.TotalSeconds > 60)
                        {
                            // don't sleep for more than minute
                            sleep = TimeSpan.FromMinutes(1);
                        }
                        sleepMilliseconds = (int)sleep.TotalMilliseconds;
                    }
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger watcher error, retrying in 10 seconds. {0}", ex.Message, ex);
                    sleepMilliseconds = 10000;
                }
                if (sleepMilliseconds < 500)
                {
                    sleepMilliseconds = 900;
                }

                while (sleepMilliseconds > 0 && !IsStopRequested)
                {
                    Thread.Sleep(Math.Min(sleepMilliseconds, 1000));
                    sleepMilliseconds -= 1000;
                }
                firstTime = false;
            }
            IsWatching = false;
            IsStopRequested = false;
            RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger watcher stopped.");
        }
    }
}
