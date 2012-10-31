// -----------------------------------------------------------------------
// <copyright file="QueueSection.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Configuration;

namespace Cinchcast.Roque.Core.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Trigger config element
    /// </summary>
    public class TriggerElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("type", IsRequired = true)]
        public string TriggerType
        {
            get { return this["type"] as string; }
            set { this["type"] = value; }
        }

        [ConfigurationProperty("queue", IsRequired = true)]
        public string Queue
        {
            get { return this["queue"] as string; }
            set { this["queue"] = value; }
        }

        [ConfigurationProperty("targetTypeFullName", IsRequired = true)]
        public string TargetTypeFullName
        {
            get { return this["targetTypeFullName"] as string; }
            set { this["targetTypeFullName"] = value; }
        }

        [ConfigurationProperty("targetMethodName", IsRequired = true)]
        public string TargetMethodName
        {
            get { return this["targetMethodName"] as string; }
            set { this["targetMethodName"] = value; }
        }

        [ConfigurationProperty("targetArgument")]
        public string TargetArgument
        {
            get { return this["targetArgument"] as string; }
            set { this["targetArgument"] = value; }
        }

        [ConfigurationProperty("settings")]
        public SettingsCollection Settings
        {
            get
            {
                return this["settings"] as SettingsCollection;
            }
        }

    }
}
