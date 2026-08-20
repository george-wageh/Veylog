using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Veylog.Models;
using Veylog.Services;

namespace Veylog.Pages;

/// <summary>
/// Page model for displaying API logs with advanced filtering and pagination.
/// </summary>
public class ApisModel : PageModel
{
    private readonly LogDbContext _db;

    public ApisModel(LogDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Filters
    // =========================================================

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

    [BindProperty(SupportsGet = true)]
    public string? Method { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusCodes { get; set; }

    // =========================================================
    // Pagination
    // =========================================================

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public const int PageSize = 25;

    // =========================================================
    // Results
    // =========================================================

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

    public int TotalPages { get; set; }

    public int TotalRecords { get; set; }

    public int TotalApis { get; set; }

    public List<ApiLog> Logs { get; set; } = new();

    // =========================================================
    // Page Load
    // =========================================================

    public async Task OnGetAsync()
    {
        PageNumber = PaginationService.ValidatePageNumber(PageNumber, int.MaxValue);

        // Build base query
        IQueryable<ApiLog> query = _db.ApiLogs.AsNoTracking();

        // Apply filters
        var filterCriteria = new ApiLogFilterService.FilterCriteria
        {
            FromDate = From,
            ToDate = To,
            Api = Api,
            Method = Method,
            StatusCodes = StatusCodes,
            RequestBody = RequestBody,
            ResponseBody = ResponseBody,
            Search = Search
        };

        query = ApiLogFilterService.ApplyFilters(query, filterCriteria);

        // Count totals
        TotalRecords = await query.CountAsync();
        TotalApis = await query.Select(x => x.Path).Distinct().CountAsync();

        // Calculate pagination
        TotalPages = PaginationService.CalculateTotalPages(TotalRecords, PageSize);
        PageNumber = PaginationService.ValidatePageNumber(PageNumber, TotalPages);

        // Return empty if no results
        if (TotalPages == 0)
        {
            PageNumber = 1;
            Logs = new List<ApiLog>();
            return;
        }

        // Get current page
        Logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(PaginationService.CalculateSkip(PageNumber, PageSize))
            .Take(PageSize)
            .ToListAsync();
    }
}