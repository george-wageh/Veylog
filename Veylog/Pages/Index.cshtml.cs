
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Pages;

public class IndexModel : PageModel
{
    private readonly LogDbContext _db;

    public IndexModel(LogDbContext db)
    {
        _db = db;
    }

    public int TotalRequests { get; set; }

    public int Errors { get; set; }

    public List<ApiLog> Logs { get; set; } = new List<ApiLog>();

    public async Task OnGetAsync()
    {
        TotalRequests = await _db.ApiLogs.CountAsync();

        Errors = await _db.ApiLogs
            .CountAsync(x => x.StatusCode >= 500);

        Logs = await _db.ApiLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync();
    }
}