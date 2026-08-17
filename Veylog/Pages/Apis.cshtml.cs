using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Pages;

public class ApisModel : PageModel
{
    private readonly LogDbContext _db;

    public ApisModel(LogDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Api { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RequestBody { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ResponseBody { get; set; }

    // Multiple HTTP methods
    [BindProperty(SupportsGet = true)]
    public List<string> Methods { get; set; } = new();

    // Multiple status codes
    // Example: 404,401,500
    [BindProperty(SupportsGet = true)]
    public string? StatusCodes { get; set; }

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

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public const int PageSize = 25;

    public int TotalPages { get; set; }

    public int TotalRecords { get; set; }

    // Number of different APIs inside filtered result
    public int TotalApis { get; set; }

    public List<ApiLog> Logs { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (PageNumber < 1)
            PageNumber = 1;

        // Normalize selected methods
        Methods = Methods
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        IQueryable<ApiLog> query = _db.ApiLogs
            .AsNoTracking();

        // Date From
        if (From.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAt >= From.Value);
        }

        // Date To
        if (To.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAt <= To.Value);
        }

        // API / Path search
        if (!string.IsNullOrWhiteSpace(Api))
        {
            query = query.Where(x =>
                x.Path.Contains(Api));
        }

        // HTTP Method multi-select
        if (Methods.Count > 0)
        {
            query = query.Where(x =>
                Methods.Contains(x.HttpMethod));
        }

        // Status Codes multi-search
        // Example:
        // 404
        // 404,401
        // 404,401,500
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

        // Request body
        if (!string.IsNullOrWhiteSpace(RequestBody))
        {
            query = query.Where(x =>
                x.RequestBody != null &&
                x.RequestBody.Contains(RequestBody));
        }

        // Response body
        if (!string.IsNullOrWhiteSpace(ResponseBody))
        {
            query = query.Where(x =>
                x.ResponseBody != null &&
                x.ResponseBody.Contains(ResponseBody));
        }

        // General search:
        // Path OR RequestBody OR ResponseBody
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x =>
                x.Path.Contains(Search) ||
                (x.RequestBody != null &&
                 x.RequestBody.Contains(Search)) ||
                (x.ResponseBody != null &&
                 x.ResponseBody.Contains(Search)));
        }

        // Total requests after filtering
        TotalRecords = await query.CountAsync();

        // Total different APIs after filtering
        TotalApis = await query
            .Select(x => x.Path)
            .Distinct()
            .CountAsync();

        // Calculate total pages
        TotalPages = (int)Math.Ceiling(
            TotalRecords / (double)PageSize);

        // If requested page is greater than last page
        if (TotalPages > 0 && PageNumber > TotalPages)
        {
            PageNumber = TotalPages;
        }

        // No results
        if (TotalPages == 0)
        {
            PageNumber = 1;
            Logs = new List<ApiLog>();
            return;
        }

        // Get current page
        Logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}