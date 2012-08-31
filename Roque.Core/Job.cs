// -----------------------------------------------------------------------
// <copyright file="Job.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Representation of a task to execute by invoking a method in a service class
    /// </summary>
    public class Job
    {
        [Newtonsoft.Json.JsonProperty("t")]
        public string Target { get; set; }

        [Newtonsoft.Json.JsonProperty("m")]
        public string Method { get; set; }

        [Newtonsoft.Json.JsonProperty("e")]
        public bool IsEvent { get; set; }

        [Newtonsoft.Json.JsonProperty("a")]
        public string[] Arguments { get; set; }

        [Newtonsoft.Json.JsonProperty("c")]
        public DateTime CreationUtc { get; set; }

        private bool _IsResuming;

        [JsonIgnore]
        public bool IsResuming
        {
            get
            {
                return _IsResuming;
            }
        }

        private Job()
        {
        }

        public static Job Create(string targetTypeFullName, string methodName, params object[] arguments)
        {
            return new Job
            {
                Target = targetTypeFullName,
                Method = methodName,
                Arguments = arguments.Select(arg => JsonConvert.SerializeObject(arg)).ToArray(),
                CreationUtc = DateTime.UtcNow
            };
        }

        public static Job Create<T>(string methodName, params object[] arguments)
            where T : new()
        {
            return Create(typeof(T).FullName, methodName, arguments);
        }

        public void MarkAsResuming()
        {
            _IsResuming = true;
        }

        public void Execute(Executor executor = null)
        {
            (executor ?? Executor.Default).Execute(this);
        }

        public static string AgeToString(TimeSpan age)
        {
            StringBuilder ageString = new StringBuilder();

            if (age.TotalSeconds < 1)
            {
                ageString.Append("< 1sec ");
            }
            else
            {
                if (age.TotalDays >= 1)
                {
                    ageString.Append((long)Math.Floor(age.TotalDays));
                    ageString.Append("days ");
                }
                if (age.Hours >= 1)
                {
                    ageString.Append(age.Hours);
                    ageString.Append("hrs ");
                }
                if (age.Minutes >= 1)
                {
                    ageString.Append(age.Minutes);
                    ageString.Append("min ");
                }
                if (age.Seconds >= 1)
                {
                    ageString.Append(age.Seconds);
                    ageString.Append("sec ");
                }
            }

            return ageString.ToString();
        }

        public string GetAgeString()
        {
            return AgeToString(DateTime.UtcNow.Subtract(CreationUtc));
        }
    }
}
