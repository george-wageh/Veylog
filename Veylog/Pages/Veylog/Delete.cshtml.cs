using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Veylog.Pages.Veylog;

public class DeleteModel : PageModel
{
    private readonly LogDbContext _db;

    public DeleteModel(LogDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // Filters
    // =========================================================

    [BindProperty]
    public DateTime? FromDate { get; set; }

    [BindProperty]
    public DateTime? ToDate { get; set; }

    [BindProperty]
    public string DeleteType { get; set; } = "Both";

    // =========================================================
    // Counts
    // =========================================================

    public int ApiLogCount { get; set; }

    public int SqlLogCount { get; set; }

    public int TotalLogCount => ApiLogCount + SqlLogCount;

    // =========================================================
    // Page Load
    // =========================================================

    public async Task OnGetAsync()
    {
        DeleteType = "Both";

        await LoadCountsAsync();
    }

    // =========================================================
    // Apply Filter
    // =========================================================

    public async Task<IActionResult> OnPostFilterAsync()
    {
        NormalizeDates();

        if (!ValidateFilters())
        {
            await LoadCountsAsync();

            return Page();
        }

        await LoadCountsAsync();

        return Page();
    }

    // =========================================================
    // Delete Selected Logs
    // =========================================================

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        NormalizeDates();

        if (!ValidateFilters())
        {
            await LoadCountsAsync();

            return Page();
        }

        var apiCount = 0;
        var sqlCount = 0;

        // -----------------------------------------------------
        // Delete API Logs
        // -----------------------------------------------------

        if (DeleteType == "Api" || DeleteType == "Both")
        {
            apiCount = await DeleteApiLogsAsync();
        }

        // -----------------------------------------------------
        // Delete SQL Logs
        // -----------------------------------------------------

        if (DeleteType == "Sql" || DeleteType == "Both")
        {
            sqlCount = await DeleteSqlLogsAsync();
        }

        // -----------------------------------------------------
        // Result Message
        // -----------------------------------------------------

        if (apiCount == 0 && sqlCount == 0)
        {
            TempData["InfoMessage"] =
                "No logs matched the selected filters.";
        }
        else
        {
            TempData["SuccessMessage"] =
                $"Deleted {apiCount:N0} API log(s) and {sqlCount:N0} SQL log(s).";
        }

        return RedirectToPage();
    }

    // =========================================================
    // Delete API Logs
    // =========================================================

    private async Task<int> DeleteApiLogsAsync()
    {
        var fromDateParameter = CreateDateParameter(
            "@FromDate",
            FromDate);

        var toDateParameter = CreateDateParameter(
            "@ToDate",
            ToDate);

        const string sql = """
            DELETE FROM [Veylog].[ApiLogs]
            WHERE
                (@FromDate IS NULL OR [CreatedAt] >= @FromDate)
                AND
                (@ToDate IS NULL OR [CreatedAt] < @ToDate)
            """;

        return await _db.Database.ExecuteSqlRawAsync(
            sql,
            fromDateParameter,
            toDateParameter);
    }

    // =========================================================
    // Delete SQL Logs
    // =========================================================

    private async Task<int> DeleteSqlLogsAsync()
    {
        var fromDateParameter = CreateDateParameter(
            "@FromDate",
            FromDate);

        var toDateParameter = CreateDateParameter(
            "@ToDate",
            ToDate);

        const string sql = """
            DELETE FROM [Veylog].[SqlLogs]
            WHERE
                (@FromDate IS NULL OR [CreatedAt] >= @FromDate)
                AND
                (@ToDate IS NULL OR [CreatedAt] < @ToDate)
            """;

        return await _db.Database.ExecuteSqlRawAsync(
            sql,
            fromDateParameter,
            toDateParameter);
    }

    // =========================================================
    // Load Counts
    // =========================================================

    private async Task LoadCountsAsync()
    {
        if (DeleteType == "Api" ||
            DeleteType == "Both")
        {
            ApiLogCount = await CountApiLogsAsync();
        }
        else
        {
            ApiLogCount = 0;
        }

        if (DeleteType == "Sql" ||
            DeleteType == "Both")
        {
            SqlLogCount = await CountSqlLogsAsync();
        }
        else
        {
            SqlLogCount = 0;
        }
    }

    // =========================================================
    // Count API Logs
    // =========================================================

    private async Task<int> CountApiLogsAsync()
    {
        var fromDateParameter = CreateDateParameter(
            "@FromDate",
            FromDate);

        var toDateParameter = CreateDateParameter(
            "@ToDate",
            ToDate);

        const string sql = """
            SELECT COUNT(*) AS [Value]
            FROM [Veylog].[ApiLogs]
            WHERE
                (@FromDate IS NULL OR [CreatedAt] >= @FromDate)
                AND
                (@ToDate IS NULL OR [CreatedAt] < @ToDate)
            """;

        var result = await _db.Database.SqlQueryRaw<int>(
            sql,
            fromDateParameter,
            toDateParameter)
            .SingleAsync();

        return result;
    }

    // =========================================================
    // Count SQL Logs
    // =========================================================

    private async Task<int> CountSqlLogsAsync()
    {
        var fromDateParameter = CreateDateParameter(
            "@FromDate",
            FromDate);

        var toDateParameter = CreateDateParameter(
            "@ToDate",
            ToDate);

        const string sql = """
            SELECT COUNT(*) AS [Value]
            FROM [Veylog].[SqlLogs]
            WHERE
                (@FromDate IS NULL OR [CreatedAt] >= @FromDate)
                AND
                (@ToDate IS NULL OR [CreatedAt] < @ToDate)
            """;

        var result = await _db.Database.SqlQueryRaw<int>(
            sql,
            fromDateParameter,
            toDateParameter)
            .SingleAsync();

        return result;
    }

    // =========================================================
    // Date Parameter
    // =========================================================

    private static SqlParameter CreateDateParameter(
        string name,
        DateTime? date)
    {
        if (!date.HasValue)
        {
            return new SqlParameter(name, DBNull.Value)
            {
                SqlDbType = System.Data.SqlDbType.DateTime2
            };
        }

        return new SqlParameter(name, date.Value)
        {
            SqlDbType = System.Data.SqlDbType.DateTime2
        };
    }

    // =========================================================
    // Normalize Dates
    // =========================================================

    private void NormalizeDates()
    {
        if (FromDate.HasValue)
        {
            FromDate = FromDate.Value.Date;
        }

        if (ToDate.HasValue)
        {
            ToDate = ToDate.Value.Date;
        }
    }

    // =========================================================
    // Validate Filters
    // =========================================================

    private bool ValidateFilters()
    {
        if (DeleteType != "Api" &&
            DeleteType != "Sql" &&
            DeleteType != "Both")
        {
            ModelState.AddModelError(
                nameof(DeleteType),
                "Please select API Logs, SQL Logs, or API + SQL.");

            return false;
        }

        if (FromDate.HasValue &&
            ToDate.HasValue &&
            FromDate.Value > ToDate.Value)
        {
            ModelState.AddModelError(
                nameof(ToDate),
                "To Date must be greater than or equal to From Date.");

            return false;
        }

        return ModelState.IsValid;
    }
}