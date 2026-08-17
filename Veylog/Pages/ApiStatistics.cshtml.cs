using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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
            Methods = Methods,
            StatusCodes = StatusCodes,
            RequestBody = RequestBody,
            ResponseBody = ResponseBody,
            Search = Search
        };

        query = ApiLogFilterService.ApplyFilters(query, filterCriteria);

        // Get required fields for statistics
        var rows = await query
            .Select(x => new StatisticsRow
            {
                Id = x.Id,
                Path = x.Path,
                CreatedAt = x.CreatedAt,
                ElapsedMilliseconds = x.ElapsedMilliseconds
            })
            .ToListAsync();

        // Calculate totals
        TotalRecords = rows.Count;
        TotalApis = rows.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        // Build and sort statistics
        var statistics = rows
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildApiStatistics(group.Key, group))
            .ToList();

        Statistics = SortStatistics(statistics);
    }
    // =========================================================
    // Sorting
    // =========================================================

    private List<ApiStatisticsGroup> SortStatistics(List<ApiStatisticsGroup> statistics)
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
    // Build Statistics
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
    // Load Available Years
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
    // Set Default Periods
    // =========================================================

    private void SetDefaultPeriods()
    {
        var today = DateTime.Today;

        // Daily defaults to last 7 days
        if (!DailyFrom.HasValue)
            DailyFrom = today.AddDays(-6);

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
        Methods = ApiLogFilterService.NormalizeMethods(Methods);

        SortBy = SortBy?.Trim().ToLowerInvariant() ?? "frequency";

        var validSortValues = new[] { "frequency", "average", "min", "max", "path" };
        if (!validSortValues.Contains(SortBy))
            SortBy = "frequency";

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

