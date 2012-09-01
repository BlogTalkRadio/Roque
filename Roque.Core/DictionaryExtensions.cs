// -----------------------------------------------------------------------
// <copyright file="DictionaryExtensions.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Utility methods to get values from an IDictionary
    /// </summary>
    public static class DictionaryExtensions
    {
        public static bool TryGet<TOutputValue, TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TOutputValue defaultValue, out TOutputValue outValue)
        {
            TValue value;
            if (!dict.TryGetValue(key, out value))
            {
                outValue = defaultValue;
                return false;
            }
            outValue = (TOutputValue)Convert.ChangeType(value, typeof(TOutputValue));
            return true;
        }

        public static bool TryGet<TOutputValue, TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, out TOutputValue outValue)
        {
            return TryGet(dict, key, default(TOutputValue), out outValue);
        }

        public static TOutputValue Get<TOutputValue, TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            return (TOutputValue)Convert.ChangeType(dict[key], typeof(TOutputValue));
        }

        public static TOutputValue Get<TOutputValue, TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TOutputValue defaultValue)
        {
            TOutputValue value;
            if (TryGet(dict, key, out value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
