// -----------------------------------------------------------------------
// <copyright file="Trace.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace Cinchcast.Roque
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Roque Trace Switch, configure using:
    /// 
    /// 
    ///   <system.diagnostics>
    ///       <switches>
    ///         <add name="Roque" value="Off|Verbose|Info|Warning|Error" />
    ///       </switches>
    ///   </system.diagnostics>
    /// 
    /// </summary>
    public static class RoqueTrace
    {
        /// <summary>
        /// Roque TraceSource, name is "roque"
        /// </summary>
        public static TraceSource Source = new TraceSource("roque", SourceLevels.All);

        /// <summary>
        /// Trace event accepting delegates. If a parameter is a delegate (eg. Func<string>) its evaluated and the return value is used on its place.
        /// Useful when parameters are expensive to obtain
        /// </summary>
        /// <param name="source"></param>
        /// <param name="eventType"></param>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        public static void Trace(this TraceSource source, TraceEventType eventType, int id, string format, params object[] parameters)
        {
            if (source.Switch.ShouldTrace(eventType))
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i] is Delegate)
                    {
                        parameters[i] = ((Delegate)parameters[i]).DynamicInvoke();
                    }
                }
                source.TraceEvent(eventType, id, format, parameters);
            }
        }

        /// <summary>
        /// Trace event accepting delegates. If a parameter is a delegate (eg. Func<string>) its evaluated and the return value is used on its place.
        /// Useful when parameters are expensive to obtain
        /// </summary>
        /// <param name="source"></param>
        /// <param name="eventType"></param>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        public static void Trace(this TraceSource source, TraceEventType eventType, string format, params object[] parameters)
        {
            if (source.Switch.ShouldTrace(eventType))
            {
                source.Trace(eventType, -1, format, parameters);
            }
        }

    }
}
