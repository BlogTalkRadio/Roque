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
    /// Queue config element
    /// </summary>
    public class QueueElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("type")]
        public string QueueType
        {
            get { return this["type"] as string; }
            set { this["type"] = value; }
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
