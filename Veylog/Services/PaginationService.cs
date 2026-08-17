namespace Veylog.Services;

/// <summary>
/// Service for handling pagination calculations.
/// Centralizes logic for calculating page numbers and totals.
/// </summary>
public static class PaginationService
{
    /// <summary>
    /// Calculates total pages based on total records and page size.
    /// </summary>
    public static int CalculateTotalPages(int totalRecords, int pageSize)
    {
        return (int)Math.Ceiling(totalRecords / (double)pageSize);
    }

    /// <summary>
    /// Validates and adjusts the page number to be within valid range.
    /// </summary>
    public static int ValidatePageNumber(int pageNumber, int totalPages)
    {
        if (pageNumber < 1)
            return 1;

        if (totalPages > 0 && pageNumber > totalPages)
            return totalPages;

        return pageNumber;
    }

    /// <summary>
    /// Calculates the number of records to skip for a given page.
    /// </summary>
    public static int CalculateSkip(int pageNumber, int pageSize)
    {
        return (pageNumber - 1) * pageSize;
    }
}
