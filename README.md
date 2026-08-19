# Veylog

![Veylog](https://img.shields.io/badge/Version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-7.0%20%7C%208.0%20%7C%209.0%20%7C%2010.0-green)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Veylog is a comprehensive logging and monitoring library for ASP.NET Core applications. It provides detailed API and SQL query logging with performance metrics, authentication for the logging interface, database integration, and an interactive dashboard.

---

## Features

### API Logging
- Automatically logs all incoming HTTP requests and responses
- Records request method, URL, headers, and query parameters
- Captures response status codes and content
- Tracks request duration for performance analysis
- Stores user information (if authenticated) and IP address
- Supports request/response body logging

### SQL Query Logging
- Intercepts and logs all SQL queries executed by Entity Framework Core
- Records query execution time with millisecond precision
- Supports three SQL operation types:
  - `Reader` (SELECT queries)
  - `NonQuery` (INSERT/UPDATE/DELETE queries)
  - `Scalar` (COUNT/MAX/MIN/SUM queries)
- Logs query parameters for debugging
- Captures SQL errors and exceptions

### Authentication & Security
- Token-based authentication for the logging dashboard
- Secure cookie-based session with 8-hour expiration
- Automatic token generation and validation
- Sensitive data masking (passwords, tokens, cards, authorization headers)
- Protected dashboard route (`/veylog/login`)

### Performance Monitoring
- Tracks query execution time
- Calculates summary statistics (min, max, average)
- Builds daily, monthly, and yearly statistics
- Monitors slow queries for performance optimization

### Database Integration
- Uses Entity Framework Core with SQL Server
- Async background logging with batching
- Configurable flush interval (seconds) and batch size
- Automatic database migration on startup
- Non-blocking logging 

### Interactive Dashboard
- **API Logs View**: Detailed view of all HTTP requests with filtering capabilities
- **SQL Logs View**: View and analyze SQL queries with parameters
- **Statistics**: Visual representations of API and SQL performance
- **Filtering**:
  - Date range (from/to)
  - API path filter
  - HTTP method filter
  - Status code filter
  - Request/Response body search
  - General search across multiple fields
- **Statistics Features**:
  - Daily, monthly, and yearly statistics
  - Count, min, max, average metrics
  - API duration distribution

### Error Handling
- Automatic error tracking in API logs
- `OnError` callback for handling failed requests
- Runs from background flush service

### Configuration Options
- Toggle API logging on/off
- Toggle SQL logging on/off
- Configure request/response body logging
- Enable SQL parameter logging
- Customize flush interval and batch size
- Set expiration time for authentication tokens

### Framework Support
- **.NET 7.0**
- **.NET 8.0**
- **.NET 9.0**
- **.NET 10.0**

---

## Installation

### Using NuGet Package Manager

```powershell
Install-Package Veylog
````

### Using .NET CLI

```bash
dotnet add package Veylog
```

---

## Getting Started

### Step 1: Install the Package

```bash
dotnet add package Veylog
```

### Step 2: Configure Services

Add the Veylog services and configure options in your `Program.cs`:

```csharp
using Veylog;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext for your application
builder.Services.AddDbContext<ClientDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("ClientConnectionString"));
    // Add Veylog SQL interceptor to log SQL queries
    options.AddVeylogSqlInterceptor(sp);
});

// Register error listener (optional)
builder.Services.AddSingleton<IVeylogErrorListener, ErrorNotifierService>();
builder.Services.AddHostedService<VeylogErrorListenerBootstrapper>();

// Add Veylog services with configuration
builder.Services.AddVeylog(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ClientConnectionString")!;
    options.EnableApiLogging = true;      // Enable API logging
    options.EnableSqlLogging = true;      // Enable SQL query logging
    options.EnableRequestLogging = true;  // Log request body
    options.EnableResponseLogging = true; // Log response body
    options.EnableSqlParametersLogging = true; // Log SQL parameters
    options.FlushIntervalSeconds = 2;     // Flush logs every 2 seconds
    options.MaxBatchSize = 10;           // Maximum batch size
    // options.SlowQueryThresholdMs = 500; // Optional: Log queries slower than 500ms

    // Optional: Callback when an error log is saved
    options.OnError = async (path, id) =>
    {
        Console.WriteLine($"Error logged for path: {path}, ID: {id}");
        // You can send notifications, write to file, etc.
    };
});

var app = builder.Build();

// Use Veylog middleware
app.UseVeylog();

app.Run();
```

### Step 3: Database

The following tables will be created:

- `ApiLogs`: Stores HTTP API requests and responses
- `SqlLogs`: Stores SQL queries and their execution details

---

## Integration Examples

### Complete Startup Configuration

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Veylog;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure your DbContext with Veylog SQL Interceptor
builder.Services.AddDbContext<ClientDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("ClientConnectionString"));
    options.AddVeylogSqlInterceptor(sp);
});

// 2. Configure Veylog options
builder.Services.AddVeylog(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ClientConnectionString")!;
    options.EnableApiLogging = true;
    options.EnableSqlLogging = true;
    options.EnableSqlParametersLogging = true;
    options.FlushIntervalSeconds = 5;
    options.MaxBatchSize = 50;
});

// 3. (Optional) Add error listener
builder.Services.AddSingleton<IVeylogErrorListener, ErrorNotifierService>();
builder.Services.AddHostedService<VeylogErrorListenerBootstrapper>();

var app = builder.Build();

// 4. Enable Veylog middleware (this runs migrations automatically)
app.UseVeylog();

app.MapControllers(); // or app.MapRazorPages();
app.Run();
```

### Using VeylogTokenManager for Authentication

Generate a secure token for dashboard access:

```csharp
using Veylog;

public class VeylogService
{
    private readonly VeylogTokenManager _tokenManager;

    public VeylogService()
    {
        _tokenManager = new VeylogTokenManager();
    }

    public string GenerateToken(TimeSpan duration)
    {
        // Generate a secure 32-byte (64-character hex) token
        var token = _tokenManager.GenerateToken(duration);
        return token;
    }
}

// Usage in a controller
[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly VeylogTokenManager _tokenManager;

    public TokenController(VeylogTokenManager tokenManager)
    {
        _tokenManager = tokenManager;
    }

    [HttpGet("get-token")]
    public IActionResult GetToken()
    {
        // Generate a token with 8-hour expiration
        var token = _tokenManager.GenerateToken(TimeSpan.FromHours(8));

        return Ok(new
        {
            token = token,
            createdAt = _tokenManager.CreatedAt,
            expiresAt = _tokenManager.ExpiresAt
        });
    }
}

// Token Request model
public class TokenRequest
{
    public string Token { get; set; } = string.Empty;
}
```

### Custom Error Listener

Create a custom error listener to handle failed API requests:

```csharp
using Veylog;

public class ErrorNotifierService : IVeylogErrorListener
{
    public Task OnErrorAsync(string path, long id)
    {
        // This is called when an API request that recorded an exception
        // has had its log entry saved to the database.
        Console.WriteLine($"API Error detected at path: {path}");
        Console.WriteLine($"Log ID: {id}");

        // You can:
        // - Send email notifications
        // - Log to external systems
        // - Send Slack/Teams notifications
        // - Write to a monitoring service (e.g., Application Insights)

        return Task.CompletedTask;
    }
}

// Register in Program.cs:
builder.Services.AddSingleton<IVeylogErrorListener, ErrorNotifierService>();
builder.Services.AddHostedService<VeylogErrorListenerBootstrapper>();
```

### Conditional Logging

Disable logging for specific endpoints or request types:

```csharp
using Veylog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVeylog(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ClientConnectionString")!;
    options.EnableApiLogging = true;    // Enable by default
    options.EnableRequestLogging = true;
    options.EnableResponseLogging = true;
});

// In your middleware pipeline, you can check and skip Veylog
// for certain paths before using it
```

---

## Configuration Options Reference

### VeylogOptions

| Property | Type | Default | Description | |----------|------|---------|-------------| | `ConnectionString` | `string` | `Empty` | Database connection string for log storage | | `EnableApiLogging` | `bool` | `true` | Enable API request/response logging | | `EnableRequestLogging` | `bool` | `false` | Log HTTP request body and headers | | `EnableResponseLogging` | `bool` | `false` | Log HTTP response body | | `EnableSqlLogging` | `bool` | `true` | Enable SQL query logging | | `EnableSqlParametersLogging` | `bool` | `false` | Log SQL query parameters | | `FlushIntervalSeconds` | `int` | `30` | Interval (in seconds) to flush queued logs | | `MaxBatchSize` | `int` | `200` | Maximum number of logs in a batch before flushing | | `OnError` | `Func<string, long, Task>?` | `null` | Callback invoked when an error log is saved |

### Default Dashboard Credentials

- __Login Path__: `/veylog/login`
- __Cookie Name__: `Veylog.Auth`
- __Expiration Time__: 8 hours

No credentials are required to create the tables; authentication is enabled by default after registration.

---

## Dashboard Features

### API Logs Page

Access at `/veylog/apis`

Features:

- View all API requests with details
- Filter by date range
- Filter by API path (URL)
- Filter by HTTP method (GET, POST, PUT, DELETE, etc.)
- Filter by status code (200, 404, 500, etc.)
- Search request/response body content
- View query string parameters
- See execution time and trace ID

### SQL Logs Page

Access at `/veylog/sqls`

Features:

- View all SQL queries executed
- Filter by date range
- See query type (Reader, NonQuery, Scalar)
- View query text
- View query parameters
- See execution time and performance metrics
- Filter by success/failure status

### Statistics Page

Access at `/veylog/api-statistics`

Features:

- __Daily Statistics__: View performance trends over the last 30 days

- __Monthly Statistics__: View performance trends over the year

- __Yearly Statistics__: View yearly performance overview

- Metrics shown:

  - Count of requests/queries
  - Minimum execution time
  - Maximum execution time
  - Average execution time
  - Duration distribution chart

---

## Privacy & Security

### Sensitive Data Masking

Veylog automatically masks sensitive data in log entries:

- `password`
- `confirmPassword`
- `token`
- `accessToken`
- `refreshToken`
- `authorization`
- `cardNumber`
- `cvv`

All sensitive fields are replaced with `***MASKED***`.

### Access Control

- Authentication is required to access the dashboard
- Secure cookie-based authentication
- Session timeout configurable (default 8 hours)
- Veylog routes are excluded from logging
- CORS preflight requests are excluded from logging

---

## Troubleshooting

### Logs Not Appearing

1. Check that `EnableApiLogging` and `EnableSqlLogging` are set to `true`
2. Verify database connection string is correct
3. Ensure database migration was applied
4. Check that middleware is registered with `app.UseVeylog()`

### Authentication Issues

1. Verify the authentication cookie name: `Veylog.Auth`
2. Check that the login path is accessible: `/veylog/login`
3. Ensure token generation is working correctly

### Performance Impact

Veylog is designed to have minimal impact:

- Async background logging
- Non-blocking queue
- Configurable batch size and flush interval
- Only logs the minimum required data by default

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the [MIT License](LICENSE).

---

## Support

For issues, questions, or contributions, please visit the [GitHub repository](https://github.com/george-wageh/veylog).

---
