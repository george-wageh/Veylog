using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Pages;

/// <summary>
/// Page model for displaying detailed information about a specific API log entry.
/// Shows related SQL logs if a trace ID is available.
/// </summary>
public class ApiDetailsModel : PageModel
{
    private readonly LogDbContext _db;

    public ApiDetailsModel(LogDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Results
    // =========================================================

    public ApiLog? ApiLog { get; set; }

    public List<SqlLog> SqlLogs { get; set; } = new();

    // =========================================================
    // Page Load
    // =========================================================

    public async Task<IActionResult> OnGetAsync(long id)
    {
        ApiLog = await _db.ApiLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ApiLog == null)
            return NotFound();

        // Load related SQL logs if trace ID exists
        if (!string.IsNullOrWhiteSpace(ApiLog.TraceId))
        {
            SqlLogs = await _db.SqlLogs
                .AsNoTracking()
                .Where(x => x.TraceId == ApiLog.TraceId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        return Page();
    }
}