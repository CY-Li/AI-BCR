using System;

namespace PlustekBCR.Helpers
{
    public static class RecentPresetHelper
    {
        public static (DateTime start, DateTime end) GetRange(string? preset)
        {
            var today = DateTime.Today;
            var start = preset switch
            {
                "Today" => today,
                "Within 3 days" => today.AddDays(-2),
                "Within 7 days" => today.AddDays(-6),
                _ => today
            };

            return (start, today);
        }
    }
}
