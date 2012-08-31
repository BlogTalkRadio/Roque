// -----------------------------------------------------------------------
// <copyright file="SubscriberSection.cs" company="">
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
    /// Subscriber config element
    /// </summary>
    public class SubscriberElement : ConfigurationElement
    {
        [ConfigurationProperty("type", IsRequired = true)]
        public string SubscriberType
        {
            get { return this["type"] as string; }
            set { this["type"] = value; }
        }

        [ConfigurationProperty("sourceQueue")]
        public string SourceQueue
        {
            get { return this["sourceQueue"] as string; }
            set { this["sourceQueue"] = value; }
        }

        [ConfigurationProperty("queue")]
        public string Queue
        {
            get { return this["queue"] as string; }
            set { this["queue"] = value; }
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
