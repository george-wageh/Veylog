using Microsoft.EntityFrameworkCore;
using Veylog.Models;

namespace Veylog.Services;

/// <summary>
/// Service for building and applying filters to API log queries.
/// Eliminates duplicate filtering logic across pages.
/// </summary>
public class ApiLogFilterService
{
    public class FilterCriteria
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Api { get; set; }
        public string? Method { get; set; }
        public string? StatusCodes { get; set; }
        public string? RequestBody { get; set; }
        public string? ResponseBody { get; set; }
        public string? Search { get; set; }
    }

    /// <summary>
    /// Normalizes HTTP methods to uppercase and removes duplicates.
    /// </summary>
    public static List<string> NormalizeMethods(List<string> methods)
    {
        return methods
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Parses comma-separated status codes and returns valid integers.
    /// </summary>
    public static List<int> ParseStatusCodes(string? statusCodes)
    {
        if (string.IsNullOrWhiteSpace(statusCodes))
            return new List<int>();

        return statusCodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => int.TryParse(x, out _))
            .Select(int.Parse)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Applies all filter criteria to an API log query.
    /// </summary>
    public static IQueryable<ApiLog> ApplyFilters(
        IQueryable<ApiLog> query,
        FilterCriteria filters)
    {
        // Date range filters
        if (filters.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= filters.ToDate.Value);
        }

        // API path filter
        if (!string.IsNullOrWhiteSpace(filters.Api))
        {
            query = query.Where(x => x.Path.Contains(filters.Api));
        }

        // HTTP method filter
        if (!string.IsNullOrWhiteSpace(filters.Method))
        {
            query = query.Where(x => x.HttpMethod == filters.Method);
        }


        // Status code filter
        var statusCodes = ParseStatusCodes(filters.StatusCodes);
        if (statusCodes.Count > 0)
        {
            query = query.Where(x => statusCodes.Contains(x.StatusCode));
        }

        // Request body filter
        if (!string.IsNullOrWhiteSpace(filters.RequestBody))
        {
            query = query.Where(x =>
                x.RequestBody != null &&
                x.RequestBody.Contains(filters.RequestBody));
        }

        // Response body filter
        if (!string.IsNullOrWhiteSpace(filters.ResponseBody))
        {
            query = query.Where(x =>
                x.ResponseBody != null &&
                x.ResponseBody.Contains(filters.ResponseBody));
        }

        // General search across multiple fields
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            query = query.Where(x =>
                x.Path.Contains(filters.Search) ||
                (x.RequestBody != null && x.RequestBody.Contains(filters.Search)) ||
                (x.ResponseBody != null && x.ResponseBody.Contains(filters.Search)));
        }

        return query;
    }
}
