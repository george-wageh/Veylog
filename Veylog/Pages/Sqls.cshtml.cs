using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;
using Veylog.Services;

namespace Veylog.Pages;

/// <summary>
/// Page model for displaying SQL query logs grouped by trace ID with pagination.
/// </summary>
public class SqlsModel : PageModel
{
    private readonly LogDbContext _db;
    private const int PageSize = 20;

    public SqlsModel(LogDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Pagination
    // =========================================================

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    // =========================================================
    // Results
    // =========================================================

    public int TotalGroups { get; set; }

    public int TotalPages { get; set; }

    public List<SqlTraceGroup> Groups { get; set; } = new();

    // =========================================================
    // Page Load
    // =========================================================

    public async Task OnGetAsync()
    {
        PageNumber = PaginationService.ValidatePageNumber(PageNumber, int.MaxValue);

        // Build grouped query
        var groupedQuery = _db.SqlLogs
            .AsNoTracking()
            .GroupBy(x => x.TraceId)
            .Select(g => new SqlTraceGroup
            {
                TraceId = g.Key,
                Count = g.Count(),
                FirstCreatedAt = g.Min(x => x.CreatedAt),
                LastCreatedAt = g.Max(x => x.CreatedAt),
                TotalDuration = g.Sum(x => x.ElapsedMilliseconds),
                HasError = g.Any(x => !x.IsSuccess)
            });

        // Count total groups
        TotalGroups = await groupedQuery.CountAsync();

        // Calculate pagination
        TotalPages = PaginationService.CalculateTotalPages(TotalGroups, PageSize);
        PageNumber = PaginationService.ValidatePageNumber(PageNumber, TotalPages);

        // Return empty if no results
        if (TotalPages == 0)
        {
            PageNumber = 1;
            Groups = new List<SqlTraceGroup>();
            return;
        }

        // Get current page of groups
        Groups = await groupedQuery
            .OrderByDescending(x => x.LastCreatedAt)
            .Skip(PaginationService.CalculateSkip(PageNumber, PageSize))
            .Take(PageSize)
            .ToListAsync();

        // Load SQL logs for each group
        await LoadSqlLogsForGroupsAsync();
    }

    // =========================================================
    // Load SQL Logs
    // =========================================================

    private async Task LoadSqlLogsForGroupsAsync()
    {
        var traceIds = Groups
            .Select(x => x.TraceId)
            .Where(x => x != null)
            .ToList();

        if (traceIds.Count == 0)
            return;

        var logs = await _db.SqlLogs
            .AsNoTracking()
            .Where(x => traceIds.Contains(x.TraceId))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        foreach (var group in Groups)
        {
            group.Logs = logs
                .Where(x => x.TraceId == group.TraceId)
                .ToList();
        }
    }

    // =========================================================
    // SQL Trace Group
    // =========================================================

    public class SqlTraceGroup
    {
        public string? TraceId { get; set; }
        public int Count { get; set; }
        public DateTime FirstCreatedAt { get; set; }
        public DateTime LastCreatedAt { get; set; }
        public long TotalDuration { get; set; }
        public bool HasError { get; set; }
        public List<SqlLog> Logs { get; set; } = new();
    }
}