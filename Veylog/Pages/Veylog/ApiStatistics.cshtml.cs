using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Veylog.Models;
using Veylog.Services;

namespace Veylog.Pages;

/// <summary>
/// Page model for displaying statistics about API performance and usage.
/// Provides daily, monthly, and yearly aggregations with multiple sorting options.
/// </summary>
public class ApiStatisticsModel : PageModel
{
    private readonly LogDbContext _db;
    private readonly IMemoryCache _cache;

    private const string AvailableYearsCacheKey = "ApiStatistics_AvailableYears";
    private static readonly TimeSpan AvailableYearsCacheTtl = TimeSpan.FromMinutes(5);

    public ApiStatisticsModel(LogDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
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
    public string? Method { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusCodes { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "frequency";

    /// <summary>
    /// Number of top APIs to display (after sorting). 0 means show all.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int Top { get; set; } = 10;

    public List<int> AvailableTopCounts { get; } = new() { 10, 20, 50, 100 };

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
    // Period Filters
    // =========================================================

    [BindProperty(SupportsGet = true)]
    public DateTime? DailyFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DailyTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MonthlyYear { get; set; }

    // =========================================================
    // Results
    // =========================================================

    public List<ApiStatisticsGroup> Statistics { get; set; } = new();

    public List<int> AvailableYears { get; set; } = new();

    public int TotalApis { get; set; }

    public int TotalRecords { get; set; }

    /// <summary>
    /// Number of APIs actually rendered on the page (after applying Top).
    /// </summary>
    public int DisplayedApis => Statistics.Count;

    // =========================================================
    // Page Load
    // =========================================================

    public async Task OnGetAsync()
    {
        NormalizeFilters();
        await LoadAvailableYearsAsync();
        SetDefaultPeriods();

        // Build base query with filters
        IQueryable<ApiLog> query = _db.ApiLogs.AsNoTracking();

        var filterCriteria = new ApiLogFilterService.FilterCriteria
        {
            Api = Api,
            Method = Method,
            StatusCodes = StatusCodes,
            RequestBody = RequestBody,
            ResponseBody = ResponseBody,
            Search = Search
        };

        query = ApiLogFilterService.ApplyFilters(query, filterCriteria);

        // -----------------------------------------------------
        // Phase 1: cheap aggregation done in SQL.
        // We only need per-path count/avg/min/max to know totals
        // and to sort — NOT the full row set, and NOT the
        // daily/monthly/yearly breakdown, which is expensive.
        // -----------------------------------------------------
        var perPathAggregates = await query
            .GroupBy(x => x.Path)
            .Select(g => new PathAggregate
            {
                Path = g.Key,
                Count = g.Count(),
                AverageMs = g.Average(x => x.ElapsedMilliseconds),
                MinMs = g.Min(x => x.ElapsedMilliseconds),
                MaxMs = g.Max(x => x.ElapsedMilliseconds)
            })
            .ToListAsync();

        // Totals come from the cheap aggregate, no need to materialize raw rows.
        TotalApis = perPathAggregates.Count;
        TotalRecords = perPathAggregates.Sum(x => x.Count);

        // Sort using only the cheap aggregate fields.
        var orderedAggregates = SortAggregates(perPathAggregates);

        // Apply Top BEFORE doing any expensive per-row work.
        var limitedAggregates = Top > 0
            ? orderedAggregates.Take(Top).ToList()
            : orderedAggregates;

        if (limitedAggregates.Count == 0)
        {
            Statistics = new List<ApiStatisticsGroup>();
            return;
        }

        // -----------------------------------------------------
        // Phase 2: pull raw rows ONLY for the paths we're
        // actually going to display, then build the full
        // daily/monthly/yearly breakdowns for just those.
        // -----------------------------------------------------
        var topPaths = limitedAggregates.Select(x => x.Path).ToList();

        var detailRows = await query
            .Where(x => topPaths.Contains(x.Path))
            .Select(x => new StatisticsRow
            {
                Id = x.Id,
                Path = x.Path,
                CreatedAt = x.CreatedAt,
                ElapsedMilliseconds = x.ElapsedMilliseconds
            })
            .ToListAsync();

        var rowsByPath = detailRows
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IEnumerable<StatisticsRow>)g, StringComparer.OrdinalIgnoreCase);

        // Preserve the sort order established in Phase 1.
        Statistics = limitedAggregates
            .Select(agg => BuildApiStatistics(
                agg.Path,
                rowsByPath.TryGetValue(agg.Path, out var rows) ? rows : Enumerable.Empty<StatisticsRow>()))
            .ToList();
    }

    // =========================================================
    // Sorting (cheap, pre-detail sort over aggregates)
    // =========================================================

    private List<PathAggregate> SortAggregates(List<PathAggregate> aggregates)
    {
        return SortBy?.Trim().ToLowerInvariant() switch
        {
            "frequency" =>
                aggregates
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            "average" =>
                aggregates
                    .OrderByDescending(x => x.AverageMs)
                    .ThenByDescending(x => x.Count)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            "min" =>
                aggregates
                    .OrderBy(x => x.MinMs)
                    .ThenByDescending(x => x.Count)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            "max" =>
                aggregates
                    .OrderByDescending(x => x.MaxMs)
                    .ThenByDescending(x => x.Count)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            "path" =>
                aggregates
                    .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

            _ =>
                aggregates
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
    }

    // =========================================================
    // Build Statistics (only called for the Top N paths)
    // =========================================================

    private ApiStatisticsGroup BuildApiStatistics(
        string apiPath,
        IEnumerable<StatisticsRow> rows)
    {
        var rowList = rows.ToList();

        // Convert local StatisticsRow to service StatisticsRow
        var serviceRows = rowList.Select(r => new StatisticsCalculationService.StatisticsRow
        {
            Id = r.Id,
            ElapsedMilliseconds = r.ElapsedMilliseconds,
            CreatedAt = r.CreatedAt
        }).ToList();

        return new ApiStatisticsGroup
        {
            Path = apiPath,
            TotalRequests = rowList.Count,
            Overall = StatisticsCalculationService.CalculateSummary(serviceRows),
            Daily = StatisticsCalculationService.BuildDailyStatistics(
                serviceRows,
                DailyFrom!.Value,
                DailyTo!.Value),
            Monthly = StatisticsCalculationService.BuildMonthlyStatistics(
                serviceRows,
                MonthlyYear!.Value),
            Yearly = StatisticsCalculationService.BuildYearlyStatistics(serviceRows)
        };
    }

    // =========================================================
    // Load Available Years (cached — full-table scan is expensive
    // on large tables and this data rarely changes second-to-second)
    // =========================================================

    private async Task LoadAvailableYearsAsync()
    {
        if (_cache.TryGetValue(AvailableYearsCacheKey, out List<int>? cachedYears) && cachedYears is not null)
        {
            AvailableYears = cachedYears;
            return;
        }

        var years = await _db.ApiLogs
            .AsNoTracking()
            .Select(x => x.CreatedAt.Year)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync();

        _cache.Set(AvailableYearsCacheKey, years, AvailableYearsCacheTtl);
        AvailableYears = years;
    }

    // =========================================================
    // Set Default Periods
    // =========================================================

    private void SetDefaultPeriods()
    {
        var today = DateTime.Today;

        // Daily defaults to the current month (1st of this month through today)
        if (!DailyFrom.HasValue)
            DailyFrom = new DateTime(today.Year, today.Month, 1);

        if (!DailyTo.HasValue)
            DailyTo = today;

        // Swap if backwards
        if (DailyFrom > DailyTo)
            (DailyFrom, DailyTo) = (DailyTo, DailyFrom);

        // Monthly defaults to current year
        if (!MonthlyYear.HasValue)
            MonthlyYear = today.Year;

        // If selected year doesn't exist, use newest available year
        if (AvailableYears.Count > 0 && !AvailableYears.Contains(MonthlyYear.Value))
            MonthlyYear = AvailableYears.First();
    }

    // =========================================================
    // Normalize Filters
    // =========================================================

    private void NormalizeFilters()
    {
        SortBy = SortBy?.Trim().ToLowerInvariant() ?? "frequency";

        var validSortValues = new[] { "frequency", "average", "min", "max", "path" };
        if (!validSortValues.Contains(SortBy))
            SortBy = "frequency";

        // Normalize Top: only allow known values or 0 (All); anything else falls back to default
        if (Top != 0 && !AvailableTopCounts.Contains(Top))
            Top = 10;

        // Normalize status codes
        var statusCodeList = ApiLogFilterService.ParseStatusCodes(StatusCodes);
        StatusCodes = statusCodeList.Count > 0 ? string.Join(",", statusCodeList) : null;
    }

    // =========================================================
    // JSON Helper
    // =========================================================

    public string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    // =========================================================
    // Statistics Row
    // =========================================================

    private class StatisticsRow
    {
        public long Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }

    // =========================================================
    // Cheap Per-Path Aggregate (computed in SQL, used for
    // totals + sorting BEFORE the expensive detail build)
    // =========================================================

    private class PathAggregate
    {
        public string Path { get; set; } = string.Empty;
        public int Count { get; set; }
        public double AverageMs { get; set; }
        public long MinMs { get; set; }
        public long MaxMs { get; set; }
    }

    // =========================================================
    // API Statistics Group
    // =========================================================

    public class ApiStatisticsGroup
    {
        public string Path { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public StatisticsCalculationService.StatisticsSummary Overall { get; set; } = new();
        public List<StatisticsCalculationService.StatisticsPoint> Daily { get; set; } = new();
        public List<StatisticsCalculationService.StatisticsPoint> Monthly { get; set; } = new();
        public List<StatisticsCalculationService.StatisticsPoint> Yearly { get; set; } = new();
    }
}