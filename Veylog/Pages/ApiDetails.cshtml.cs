using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Pages;

public class ApiDetailsModel : PageModel
{
    private readonly LogDbContext _db;

    public ApiDetailsModel(LogDbContext db)
    {
        _db = db;
    }

    public ApiLog? ApiLog { get; set; }

    public List<SqlLog> SqlLogs { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        ApiLog = await _db.ApiLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ApiLog == null)
        {
            return NotFound();
        }

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