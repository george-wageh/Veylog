namespace Veylog.Services;

/// <summary>
/// Service for calculating statistics on collections of data.
/// Used for building daily, monthly, and yearly statistics.
/// </summary>
public class StatisticsCalculationService
{
    public class StatisticsSummary
    {
        public int Count { get; set; }
        public long Min { get; set; }
        public long Max { get; set; }
        public double Average { get; set; }
        public long? MinId { get; set; }
        public long? MaxId { get; set; }
    }

    public class StatisticsPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public long Min { get; set; }
        public long Max { get; set; }
        public double Average { get; set; }
        public long? MinId { get; set; }
        public long? MaxId { get; set; }
    }

    /// <summary>
    /// Represents a single data row for statistics calculation.
    /// </summary>
    public class StatisticsRow
    {
        public long Id { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Calculates overall summary statistics from a collection of rows.
    /// </summary>
    public static StatisticsSummary CalculateSummary(IEnumerable<StatisticsRow> rows)
    {
        var list = rows.ToList();

        if (!list.Any())
        {
            return new StatisticsSummary();
        }

        var minRow = list
            .OrderBy(x => x.ElapsedMilliseconds)
            .ThenBy(x => x.Id)
            .First();

        var maxRow = list
            .OrderByDescending(x => x.ElapsedMilliseconds)
            .ThenBy(x => x.Id)
            .First();

        return new StatisticsSummary
        {
            Count = list.Count,
            Min = minRow.ElapsedMilliseconds,
            Max = maxRow.ElapsedMilliseconds,
            Average = list.Average(x => x.ElapsedMilliseconds),
            MinId = minRow.Id,
            MaxId = maxRow.Id
        };
    }

    /// <summary>
    /// Calculates statistics for a specific point/period from a collection of rows.
    /// </summary>
    public static StatisticsPoint CalculatePoint(
        string label,
        IEnumerable<StatisticsRow> rows)
    {
        var list = rows.ToList();

        if (!list.Any())
        {
            return new StatisticsPoint
            {
                Label = label,
                Count = 0,
                Min = 0,
                Max = 0,
                Average = 0,
                MinId = null,
                MaxId = null
            };
        }

        var minRow = list
            .OrderBy(x => x.ElapsedMilliseconds)
            .ThenBy(x => x.Id)
            .First();

        var maxRow = list
            .OrderByDescending(x => x.ElapsedMilliseconds)
            .ThenBy(x => x.Id)
            .First();

        return new StatisticsPoint
        {
            Label = label,
            Count = list.Count,
            Min = minRow.ElapsedMilliseconds,
            Max = maxRow.ElapsedMilliseconds,
            Average = list.Average(x => x.ElapsedMilliseconds),
            MinId = minRow.Id,
            MaxId = maxRow.Id
        };
    }

    /// <summary>
    /// Builds daily statistics for a date range, filling gaps with zero values.
    /// </summary>
    public static List<StatisticsPoint> BuildDailyStatistics(
        List<StatisticsRow> rows,
        DateTime from,
        DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        var filtered = rows
            .Where(x => x.CreatedAt.Date >= fromDate && x.CreatedAt.Date <= toDate)
            .ToList();

        var grouped = filtered
            .GroupBy(x => x.CreatedAt.Date)
            .ToDictionary(
                x => x.Key,
                x => CalculatePoint(
                    x.Key.ToString("yyyy-MM-dd"),
                    x));

        var result = new List<StatisticsPoint>();

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            if (grouped.TryGetValue(date, out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new StatisticsPoint
                {
                    Label = date.ToString("yyyy-MM-dd"),
                    Count = 0,
                    Min = 0,
                    Max = 0,
                    Average = 0,
                    MinId = null,
                    MaxId = null
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Builds monthly statistics for a specific year, filling gaps with zero values.
    /// </summary>
    public static List<StatisticsPoint> BuildMonthlyStatistics(
        List<StatisticsRow> rows,
        int year)
    {
        var filtered = rows
            .Where(x => x.CreatedAt.Year == year)
            .ToList();

        var grouped = filtered
            .GroupBy(x => x.CreatedAt.Month)
            .ToDictionary(
                x => x.Key,
                x => CalculatePoint(
                    new DateTime(year, x.Key, 1).ToString("yyyy-MM"),
                    x));

        var result = new List<StatisticsPoint>();

        for (var month = 1; month <= 12; month++)
        {
            if (grouped.TryGetValue(month, out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new StatisticsPoint
                {
                    Label = new DateTime(year, month, 1).ToString("yyyy-MM"),
                    Count = 0,
                    Min = 0,
                    Max = 0,
                    Average = 0,
                    MinId = null,
                    MaxId = null
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Builds yearly statistics from a collection of rows.
    /// </summary>
    public static List<StatisticsPoint> BuildYearlyStatistics(List<StatisticsRow> rows)
    {
        if (!rows.Any())
        {
            return new List<StatisticsPoint>();
        }

        return rows
            .GroupBy(x => x.CreatedAt.Year)
            .OrderBy(x => x.Key)
            .Select(x => CalculatePoint(x.Key.ToString(), x))
            .ToList();
    }
}
