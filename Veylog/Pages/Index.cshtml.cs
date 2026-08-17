
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Pages;

/// <summary>
/// Dashboard page model displaying key metrics and recent API logs.
/// </summary>
public class IndexModel : PageModel
{
    private readonly LogDbContext _db;
    private const int RecentLogsCount = 20;

    public IndexModel(LogDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Results
    // =========================================================

    public int TotalRequests { get; set; }

    public int Errors { get; set; }

    public List<ApiLog> Logs { get; set; } = new();

    // =========================================================
    // Page Load
    // =========================================================

    public async Task OnGetAsync()
    {
        TotalRequests = await _db.ApiLogs.CountAsync();

        Errors = await _db.ApiLogs
            .CountAsync(x => x.StatusCode >= 500);

        Logs = await _db.ApiLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(RecentLogsCount)
            .ToListAsync();
    }
}