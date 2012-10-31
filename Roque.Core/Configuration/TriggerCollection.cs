// -----------------------------------------------------------------------
// <copyright file="QueueCollection.cs" company="">
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
    /// Trigger collection config element
    /// </summary>
    public class TriggerCollection : ConfigurationElementCollection
    {
        public TriggerElement this[object key]
        {
            get
            {
                return base.BaseGet(key) as TriggerElement;
            }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "trigger";
            }
        }

        protected override bool IsElementName(string elementName)
        {
            bool isName = false;
            if (!String.IsNullOrEmpty(elementName))
                isName = elementName.Equals("trigger");
            return isName;
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new TriggerElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((TriggerElement)element).Name;
        }
    }
}
