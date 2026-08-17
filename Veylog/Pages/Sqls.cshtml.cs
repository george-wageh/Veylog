using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Pages;

public class SqlsModel : PageModel
{
    private readonly LogDbContext _db;

    public SqlsModel(LogDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public const int PageSize = 20;

    public int TotalGroups { get; set; }

    public int TotalPages { get; set; }

    public List<SqlTraceGroup> Groups { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (PageNumber < 1)
            PageNumber = 1;

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

        TotalGroups = await groupedQuery.CountAsync();

        TotalPages = (int)Math.Ceiling(
            TotalGroups / (double)PageSize);

        if (TotalPages > 0 && PageNumber > TotalPages)
            PageNumber = TotalPages;

        if (TotalPages == 0)
        {
            PageNumber = 1;
            Groups = new List<SqlTraceGroup>();
            return;
        }

        Groups = await groupedQuery
            .OrderByDescending(x => x.LastCreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

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