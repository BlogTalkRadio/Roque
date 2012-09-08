using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Cinchcast.Roque.Core
{
    public class FileWatcher
    {
        public void OnConfigOrDllChanges(Action action, bool onlyOnce = false)
        {
            var file = new FileInfo(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);

            var watcherDlls = new FileSystemWatcher(file.Directory.FullName)
            {
                IncludeSubdirectories = false,
                Filter = "*.dll",
                NotifyFilter = NotifyFilters.LastWrite
            };
            var watcherConfigs = new FileSystemWatcher(file.Directory.FullName)
            {
                IncludeSubdirectories = false,
                Filter = "*.config",
                NotifyFilter = NotifyFilters.LastWrite
            };

            bool fired = false;

            FileSystemEventHandler onFileChange = (sender, ea) =>
                {
                    if (fired && onlyOnce)
                    {
                        return;
                    }
                    fired = true;
                    if (onlyOnce)
                    {
                        watcherDlls.EnableRaisingEvents = false;
                        watcherConfigs.EnableRaisingEvents = false;
                        watcherDlls.Dispose();
                        watcherConfigs.Dispose();
                    }
                    Trace.TraceInformation("[FileWatcher] file change detected: {0} ({1})", ea.Name, ea.ChangeType);
                    action();
                };

            watcherDlls.Changed += onFileChange;
            watcherConfigs.Changed += onFileChange;

            watcherDlls.EnableRaisingEvents = true;
            watcherConfigs.EnableRaisingEvents = true;
        }
    }
}
