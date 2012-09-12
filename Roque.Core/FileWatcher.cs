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
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
            var watcherConfigs = new FileSystemWatcher(file.Directory.FullName)
            {
                IncludeSubdirectories = false,
                Filter = "*.config",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            bool fired = false;

            FileSystemEventHandler onFileChange = null;
            RenamedEventHandler onFileRename = null;

            Action<FileSystemEventArgs> onChange = (ea) =>
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

                        watcherDlls.Created -= onFileChange;
                        watcherDlls.Changed -= onFileChange;
                        watcherDlls.Deleted -= onFileChange;
                        watcherDlls.Renamed -= onFileRename;

                        watcherConfigs.Created -= onFileChange;
                        watcherConfigs.Changed -= onFileChange;
                        watcherConfigs.Deleted -= onFileChange;
                        watcherConfigs.Renamed -= onFileRename;

                        watcherDlls.Dispose();
                        watcherConfigs.Dispose();
                    }
                    Trace.TraceInformation("[FileWatcher] file change detected: {0} ({1})", ea.Name, ea.ChangeType);
                    action();
                };

            onFileChange = (sender, ea) =>
                {
                    onChange(ea);
                };
            onFileRename = (sender, ea) =>
                {
                    onChange(ea);
                };

            watcherDlls.Created += onFileChange;
            watcherDlls.Changed += onFileChange;
            watcherDlls.Deleted += onFileChange;
            watcherDlls.Renamed += onFileRename;

            watcherConfigs.Created += onFileChange;
            watcherConfigs.Changed += onFileChange;
            watcherConfigs.Deleted += onFileChange;
            watcherConfigs.Renamed += onFileRename;

            watcherDlls.EnableRaisingEvents = true;
            watcherConfigs.EnableRaisingEvents = true;
        }
    }
}
