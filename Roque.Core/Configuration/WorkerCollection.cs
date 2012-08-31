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
    /// Queue collection config element
    /// </summary>
    public class WorkerCollection : ConfigurationElementCollection
    {
        public WorkerElement this[object key]
        {
            get
            {
                return base.BaseGet(key) as WorkerElement;
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
                return "worker";
            }
        }

        protected override bool IsElementName(string elementName)
        {
            bool isName = false;
            if (!String.IsNullOrEmpty(elementName))
                isName = elementName.Equals("worker");
            return isName;
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new WorkerElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((WorkerElement)element).Name;
        }
    }
}
