using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Redis;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Trigger that executes when a file is modified or created in a folder.
    /// </summary>
    public class FileWatcherTrigger : Trigger
    {
        protected Func<DateTime?, DateTime?> NextExecutionGetter;

        protected override DateTime? GetNextExecution(DateTime? lastExecution)
        {
            if (NextExecutionGetter == null)
            {
                var folder = Settings.Get<string, string, string>("folder");
                var interval = Settings.Get("intervalSeconds", 30);
                if (interval <= 0)
                {
                    throw new Exception("Interval must be bigger than zero");
                }

                NextExecutionGetter = (lastExec) =>
                    {
                        if (lastExec == null)
                        {
                            return DateTime.UtcNow;
                        }
                        var directory = new DirectoryInfo(folder);
                        if (directory.Exists)
                        {
                            var newestFile = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                            if (newestFile != null &&
                                (newestFile.LastWriteTimeUtc >= lastExec || newestFile.CreationTimeUtc >= lastExec))
                            {
                                // there are new files, execute trigger
                                return DateTime.UtcNow;
                            }
                        }
                        return DateTime.UtcNow.AddSeconds(interval);
                    };
            }
            return NextExecutionGetter(lastExecution);
        }
    }
}
