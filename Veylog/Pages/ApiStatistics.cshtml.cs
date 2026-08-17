using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Veylog.Models;

namespace Veylog.Pages;

public class ApiStatisticsModel : PageModel
{
    private readonly LogDbContext _db;

    public ApiStatisticsModel(LogDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Filters
    // =========================================================

    [BindProperty(SupportsGet = true)]
    public string? Api { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RequestBody { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ResponseBody { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> Methods { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusCodes { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "frequency";

    public List<string> AvailableMethods { get; } = new()
    {
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "HEAD",
        "OPTIONS"
    };


    // =========================================================
    // Daily
    // =========================================================

    [BindProperty(SupportsGet = true)]
    public DateTime? DailyFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DailyTo { get; set; }


    // =========================================================
    // Monthly
    // =========================================================

    [BindProperty(SupportsGet = true)]
    public int? MonthlyYear { get; set; }


    // =========================================================
    // Results
    // =========================================================

    public List<ApiStatisticsGroup> Statistics { get; set; } = new();

    public List<int> AvailableYears { get; set; } = new();

    public int TotalApis { get; set; }

    public int TotalRecords { get; set; }


    // =========================================================
    // GET
    // =========================================================

    public async Task OnGetAsync()
    {
        NormalizeFilters();

        await LoadAvailableYearsAsync();

        SetDefaultPeriods();

        IQueryable<ApiLog> query = _db.ApiLogs
            .AsNoTracking();


        // =====================================================
        // API / Path
        // =====================================================

        if (!string.IsNullOrWhiteSpace(Api))
        {
            query = query.Where(x =>
                x.Path.Contains(Api));
        }


        // =====================================================
        // HTTP Methods
        // =====================================================

        if (Methods.Count > 0)
        {
            query = query.Where(x =>
                Methods.Contains(x.HttpMethod));
        }


        // =====================================================
        // Status Codes
        // =====================================================

        if (!string.IsNullOrWhiteSpace(StatusCodes))
        {
            var statusCodes = StatusCodes
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(x => int.TryParse(x, out _))
                .Select(int.Parse)
                .Distinct()
                .ToList();

            if (statusCodes.Count > 0)
            {
                query = query.Where(x =>
                    statusCodes.Contains(x.StatusCode));
            }
        }


        // =====================================================
        // Request Body
        // =====================================================

        if (!string.IsNullOrWhiteSpace(RequestBody))
        {
            query = query.Where(x =>
                x.RequestBody != null &&
                x.RequestBody.Contains(RequestBody));
        }


        // =====================================================
        // Response Body
        // =====================================================

        if (!string.IsNullOrWhiteSpace(ResponseBody))
        {
            query = query.Where(x =>
                x.ResponseBody != null &&
                x.ResponseBody.Contains(ResponseBody));
        }


        // =====================================================
        // General Search
        // =====================================================

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                x.Path.Contains(Search) ||
                (x.RequestBody != null &&
                 x.RequestBody.Contains(Search)) ||
                (x.ResponseBody != null &&
                 x.ResponseBody.Contains(Search)));
        }


        // =====================================================
        // Get required fields
        // IMPORTANT:
        // We now get Id as well.
        // =====================================================

        var rows = await query
            .Select(x => new StatisticsRow
            {
                Id = x.Id,

                Path = x.Path,

                CreatedAt = x.CreatedAt,

                ElapsedMilliseconds =
                    x.ElapsedMilliseconds
            })
            .ToListAsync();


        // =====================================================
        // Overall totals
        // =====================================================

        TotalRecords = rows.Count;

        TotalApis = rows
            .Select(x => x.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();


        // =====================================================
        // Build statistics per API
        // =====================================================

        var statistics = rows
            .GroupBy(
                x => x.Path,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                BuildApiStatistics(
                    group.Key,
                    group))
            .ToList();


        // =====================================================
        // Sort APIs
        // =====================================================

        Statistics = SortStatistics(statistics);
    }


    // =========================================================
    // Sort Statistics
    // =========================================================

    private List<ApiStatisticsGroup> SortStatistics(
        List<ApiStatisticsGroup> statistics)
    {
        return SortBy?.Trim().ToLowerInvariant() switch
        {
            "frequency" =>
                statistics
                    .OrderByDescending(x => x.TotalRequests)
                    .ThenBy(x => x.Path)
                    .ToList(),

            "average" =>
                statistics
                    .OrderByDescending(x => x.Overall.Average)
                    .ThenByDescending(x => x.TotalRequests)
                    .ThenBy(x => x.Path)
                    .ToList(),

            "min" =>
                statistics
                    .OrderBy(x => x.Overall.Min)
                    .ThenByDescending(x => x.TotalRequests)
                    .ThenBy(x => x.Path)
                    .ToList(),

            "max" =>
                statistics
                    .OrderByDescending(x => x.Overall.Max)
                    .ThenByDescending(x => x.TotalRequests)
                    .ThenBy(x => x.Path)
                    .ToList(),

            "path" =>
                statistics
                    .OrderBy(x => x.Path)
                    .ToList(),

            _ =>
                statistics
                    .OrderByDescending(x => x.TotalRequests)
                    .ThenBy(x => x.Path)
                    .ToList()
        };
    }


    // =========================================================
    // Build API Statistics
    // =========================================================

    private ApiStatisticsGroup BuildApiStatistics(
        string apiPath,
        IEnumerable<StatisticsRow> rows)
    {
        var rowList = rows.ToList();

        return new ApiStatisticsGroup
        {
            Path = apiPath,

            TotalRequests = rowList.Count,

            Overall = BuildSummary(rowList),

            Daily = BuildDailyStatistics(rowList),

            Monthly = BuildMonthlyStatistics(
                rowList,
                MonthlyYear!.Value),

            Yearly = BuildYearlyStatistics(rowList)
        };
    }


    // =========================================================
    // Daily Statistics
    // =========================================================

    private List<StatisticsPoint> BuildDailyStatistics(
        List<StatisticsRow> rows)
    {
        var from = DailyFrom!.Value.Date;

        var to = DailyTo!.Value.Date;


        var filtered = rows
            .Where(x =>
                x.CreatedAt.Date >= from &&
                x.CreatedAt.Date <= to)
            .ToList();


        var grouped = filtered
            .GroupBy(x => x.CreatedAt.Date)
            .ToDictionary(
                x => x.Key,
                x => BuildPoint(
                    x.Key.ToString("yyyy-MM-dd"),
                    x));


        var result = new List<StatisticsPoint>();


        for (
            var date = from;
            date <= to;
            date = date.AddDays(1))
        {
            if (grouped.TryGetValue(
                date,
                out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new StatisticsPoint
                {
                    Label =
                        date.ToString("yyyy-MM-dd"),

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


    // =========================================================
    // Monthly Statistics
    // =========================================================

    private List<StatisticsPoint> BuildMonthlyStatistics(
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
                x => BuildPoint(
                    new DateTime(
                        year,
                        x.Key,
                        1)
                        .ToString("yyyy-MM"),
                    x));


        var result = new List<StatisticsPoint>();


        for (var month = 1; month <= 12; month++)
        {
            if (grouped.TryGetValue(
                month,
                out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new StatisticsPoint
                {
                    Label =
                        new DateTime(
                            year,
                            month,
                            1)
                            .ToString("yyyy-MM"),

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


    // =========================================================
    // Yearly Statistics
    // =========================================================

    private List<StatisticsPoint> BuildYearlyStatistics(
        List<StatisticsRow> rows)
    {
        if (!rows.Any())
        {
            return new List<StatisticsPoint>();
        }


        return rows
            .GroupBy(x => x.CreatedAt.Year)
            .OrderBy(x => x.Key)
            .Select(x =>
                BuildPoint(
                    x.Key.ToString(),
                    x))
            .ToList();
    }


    // =========================================================
    // Overall Summary
    // =========================================================

    private StatisticsSummary BuildSummary(
        IEnumerable<StatisticsRow> rows)
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

            Average = list.Average(
                x => x.ElapsedMilliseconds),

            MinId = minRow.Id,

            MaxId = maxRow.Id
        };
    }


    // =========================================================
    // Statistics Point
    // =========================================================

    private StatisticsPoint BuildPoint(
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


        // =============================================
        // Request having minimum elapsed time
        // =============================================

        var minRow = list
            .OrderBy(x => x.ElapsedMilliseconds)
            .ThenBy(x => x.Id)
            .First();


        // =============================================
        // Request having maximum elapsed time
        // =============================================

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

            Average = list.Average(
                x => x.ElapsedMilliseconds),

            MinId = minRow.Id,

            MaxId = maxRow.Id
        };
    }


    // =========================================================
    // Available Years
    // =========================================================

    private async Task LoadAvailableYearsAsync()
    {
        AvailableYears = await _db.ApiLogs
            .AsNoTracking()
            .Select(x => x.CreatedAt.Year)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync();
    }


    // =========================================================
    // Default Periods
    // =========================================================

    private void SetDefaultPeriods()
    {
        var today = DateTime.Today;


        // Daily defaults to last 7 days

        if (!DailyFrom.HasValue)
        {
            DailyFrom =
                today.AddDays(-6);
        }


        if (!DailyTo.HasValue)
        {
            DailyTo = today;
        }


        // Swap if backwards

        if (DailyFrom > DailyTo)
        {
            (DailyFrom, DailyTo) =
                (DailyTo, DailyFrom);
        }


        // Monthly defaults to current year

        if (!MonthlyYear.HasValue)
        {
            MonthlyYear =
                today.Year;
        }


        // If selected year doesn't exist,
        // use newest available year.

        if (AvailableYears.Count > 0 &&
            !AvailableYears.Contains(
                MonthlyYear.Value))
        {
            MonthlyYear =
                AvailableYears.First();
        }
    }


    // =========================================================
    // Normalize Filters
    // =========================================================

    private void NormalizeFilters()
    {
        Methods = Methods
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Select(x =>
                x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();


        SortBy =
            SortBy?.Trim().ToLowerInvariant()
            ?? "frequency";


        var validSortValues = new[]
        {
            "frequency",
            "average",
            "min",
            "max",
            "path"
        };


        if (!validSortValues.Contains(
            SortBy))
        {
            SortBy = "frequency";
        }


        // =============================================
        // Normalize status codes
        // =============================================

        if (!string.IsNullOrWhiteSpace(
            StatusCodes))
        {
            var validStatusCodes =
                StatusCodes
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Where(x =>
                        int.TryParse(
                            x,
                            out _))
                    .Select(x =>
                        int.Parse(x))
                    .Distinct()
                    .ToList();


            StatusCodes =
                string.Join(
                    ",",
                    validStatusCodes);
        }
        else
        {
            StatusCodes = null;
        }
    }


    // =========================================================
    // JSON Helper
    // =========================================================

    public string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            });
    }


    // =========================================================
    // Internal Query Row
    // =========================================================

    private class StatisticsRow
    {
        public long Id { get; set; }

        public string Path { get; set; } =
            string.Empty;

        public DateTime CreatedAt { get; set; }

        public long ElapsedMilliseconds { get; set; }
    }


    // =========================================================
    // Public Models
    // =========================================================

    public class ApiStatisticsGroup
    {
        public string Path { get; set; } =
            string.Empty;


        public int TotalRequests { get; set; }


        public StatisticsSummary Overall { get; set; } =
            new();


        public List<StatisticsPoint> Daily { get; set; } =
            new();


        public List<StatisticsPoint> Monthly { get; set; } =
            new();


        public List<StatisticsPoint> Yearly { get; set; } =
            new();
    }


    public class StatisticsSummary
    {
        public int Count { get; set; }


        public long Min { get; set; }


        public long Max { get; set; }


        public double Average { get; set; }


        // ID of the request that produced Min

        public long? MinId { get; set; }


        // ID of the request that produced Max

        public long? MaxId { get; set; }
    }


    public class StatisticsPoint
    {
        public string Label { get; set; } =
            string.Empty;


        public int Count { get; set; }


        public long Min { get; set; }


        public long Max { get; set; }


        public double Average { get; set; }


        // ID of request that produced Min

        public long? MinId { get; set; }


        // ID of request that produced Max

        public long? MaxId { get; set; }
    }
}
