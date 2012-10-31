using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Creates Schedules for ScheduleTrigger, parses cron expressions
    /// </summary>
    public class Schedule
    {
        private HashSet<byte> _Minute;
        private HashSet<byte> _Hour;
        private HashSet<byte> _DayOfMonth;
        private HashSet<byte> _MonthOfYear;
        private HashSet<byte> _DayOfWeek;

        private Schedule(HashSet<byte> minute, HashSet<byte> hour, HashSet<byte> dayOfMonth, HashSet<byte> monthOfYear, HashSet<byte> dayOfWeek)
        {
            _Minute = minute;
            _Hour = hour;
            _DayOfMonth = dayOfMonth;
            _MonthOfYear = monthOfYear;
            _DayOfWeek = dayOfWeek;
        }

        private static HashSet<byte> ByteSet(params byte[] values)
        {
            return new HashSet<byte>(values.AsEnumerable());
        }

        private static HashSet<byte> ByteSetRange(byte from, byte to, byte step = 1)
        {
            var hashSet = new HashSet<byte>();
            if (step < 1)
            {
                step = 1;
            }
            for (var b = Math.Min(from, to); b <= Math.Max(from, to); b += step)
            {
                hashSet.Add(b);
            }
            return hashSet;
        }

        private static HashSet<byte> ByteSet(string cronset, byte minValue, byte maxValue)
        {
            var rangeAndStep = cronset.Split('/');
            var step = (byte)(rangeAndStep.Length > 1 ? byte.Parse(rangeAndStep[1]) : 1);
            if (step < 1)
            {
                step = 1;
            }
            if (rangeAndStep[0] == "*")
            {
                return ByteSetRange(minValue, maxValue, step);
            }
            var values = rangeAndStep[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var hashSet = new HashSet<byte>();
            foreach (var value in values)
            {
                var valueRange = value.Split('-');
                if (valueRange.Length > 1)
                {
                    byte from = byte.Parse(valueRange[0]);
                    byte to = byte.Parse(valueRange[1]);
                    for (var b = Math.Min(from, to); b <= Math.Max(from, to); b += step)
                    {
                        hashSet.Add(b);
                    }
                }
                else
                {
                    hashSet.Add(byte.Parse(valueRange[0]));
                }
            }
            return hashSet;
        }

        /// <summary>
        /// Creates a Schedule using cron syntax
        /// </summary>
        /// <param name="schedule"></param>
        /// <returns></returns>
        public static Schedule Create(string schedule)
        {
            var parts = schedule.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return new Schedule(
                parts.Length > 0 ? ByteSet(parts[0], 0, 59) : null,
                parts.Length > 1 ? ByteSet(parts[1], 0, 23) : null,
                parts.Length > 2 ? ByteSet(parts[2], 1, 31) : null,
                parts.Length > 3 ? ByteSet(parts[3], 1, 12) : null,
                parts.Length > 4 ? ByteSet(parts[4], 0, 6) : null
                );
        }

        public bool IsValidExecutionTime(DateTime time)
        {
            return IsValidExecutionTime((byte)time.Month, (byte)time.Day, (byte)time.DayOfWeek, (byte)time.Hour, (byte)time.Minute);
        }

        public bool IsValidExecutionTime(byte month, byte day, byte weekDay, byte hour, byte minute)
        {
            if (_MonthOfYear != null && !_MonthOfYear.Contains(month))
            {
                return false;
            }
            if (_DayOfMonth != null && !_DayOfMonth.Contains(day))
            {
                return false;
            }
            if (_DayOfWeek != null && !_DayOfWeek.Contains(weekDay))
            {
                return false;
            }

            // the day is valid

            if (_Hour != null && !_Hour.Contains(hour))
            {
                return false;
            }
            if (_Minute != null && !_Minute.Contains(minute))
            {
                return false;
            }

            // the time is valid

            return true;
        }

        public bool IsValidExecutionDay(DateTime time)
        {
            return IsValidExecutionDay((byte)time.Month, (byte)time.Day, (byte)time.DayOfWeek);
        }

        public bool IsValidExecutionDay(byte month, byte day, byte weekDay)
        {
            if (_MonthOfYear != null && !_MonthOfYear.Contains(month))
            {
                return false;
            }
            if (_DayOfMonth != null && !_DayOfMonth.Contains(day))
            {
                return false;
            }
            if (_DayOfWeek != null && !_DayOfWeek.Contains(weekDay))
            {
                return false;
            }

            // the day is valid

            return true;
        }

        public DateTime? GetNextExecution(DateTime? lastExecution)
        {
            var last = (lastExecution ?? DateTime.UtcNow);
            var next = last.AddMinutes(1);
            next = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);

            if (IsValidExecutionDay(next))
            {
                while (!IsValidExecutionTime(next))
                {
                    next = next.AddMinutes(1);
                    if (next.Date != last.Date)
                    {
                        break;                        
                    }
                }
                if (IsValidExecutionTime(next))
                {
                    return next;
                }
            }

            // no more times on the same day, look for the next day

            var daysAdded = 0;
            while (!IsValidExecutionDay(next))
            {
                if (daysAdded > 1500)
                {
                    // no valid day found (eg. looking for Feb 31)
                    return null;
                }
                next = next.AddDays(1);
                daysAdded++;
            }
            next = new DateTime(next.Year, next.Month, next.Day,
                _Hour == null ? 0 : _Hour.Min(),
                _Minute == null ? 0 : _Minute.Min(),
                0);

            return next;
        }
    }
}
