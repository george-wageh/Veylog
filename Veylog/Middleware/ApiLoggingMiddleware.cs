using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Veylog.Models;

namespace Veylog.Middleware
{
    public class ApiLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            LogDbContext db)
        {
            var stopwatch = Stopwatch.StartNew();

            var traceId = Activity.Current?.TraceId.ToString()
                          ?? context.TraceIdentifier;

            string? requestBody = null;
            string? responseBody = null;
            string? exception = null;

            // =========================
            // Request Body
            // =========================

            if (context.Request.ContentLength > 0 &&
                context.Request.ContentType?.Contains("application/json") == true)
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    leaveOpen: true);

                requestBody = await reader.ReadToEndAsync();

                context.Request.Body.Position = 0;

                requestBody = MaskSensitiveData(requestBody);
            }

            // =========================
            // Response Body
            // =========================

            var originalResponseBody = context.Response.Body;

            await using var responseBodyStream = new MemoryStream();

            context.Response.Body = responseBodyStream;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                exception = ex.ToString();

                throw;
            }
            finally
            {
                stopwatch.Stop();

                // Read response
                context.Response.Body.Seek(0, SeekOrigin.Begin);

                using var reader = new StreamReader(
                    context.Response.Body,
                    Encoding.UTF8,
                    leaveOpen: true);

                responseBody = await reader.ReadToEndAsync();

                context.Response.Body.Seek(0, SeekOrigin.Begin);

                // Copy response to original stream
                await context.Response.Body.CopyToAsync(originalResponseBody);

                context.Response.Body = originalResponseBody;

                responseBody = MaskSensitiveData(responseBody);

                // =========================
                // Save Log
                // =========================

                try
                {
                    var log = new ApiLog
                    {
                        TraceId = traceId,

                        CreatedAt = DateTime.UtcNow,

                        HttpMethod = context.Request.Method,

                        Path = context.Request.Path,

                        QueryString = context.Request.QueryString.ToString(),

                        UserId = context.User?.Identity?.IsAuthenticated == true
                            ? context.User.FindFirst("sub")?.Value
                            : null,

                        IpAddress = context.Connection.RemoteIpAddress?.ToString(),

                        StatusCode = context.Response.StatusCode,

                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,

                        RequestBody = requestBody,

                        ResponseBody = responseBody,

                        Exception = exception
                    };

                    db.ApiLogs.Add(log);

                    await db.SaveChangesAsync();
                }
                catch
                {
                    // Never break the actual API because logging failed
                }
            }
        }

        private static string? MaskSensitiveData(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;

            try
            {
                using var document = JsonDocument.Parse(json);

                var dictionary = JsonSerializer.Deserialize<
                    Dictionary<string, object?>>(
                    json);

                if (dictionary == null)
                    return json;

                var sensitiveFields = new[]
                {
                "password",
                "confirmPassword",
                "token",
                "accessToken",
                "refreshToken",
                "authorization",
                "cardNumber",
                "cvv"
            };

                foreach (var field in sensitiveFields)
                {
                    var key = dictionary.Keys
                        .FirstOrDefault(x =>
                            x.Equals(
                                field,
                                StringComparison.OrdinalIgnoreCase));

                    if (key != null)
                    {
                        dictionary[key] = "***MASKED***";
                    }
                }

                return JsonSerializer.Serialize(dictionary);
            }
            catch
            {
                return json;
            }
        }
    }
}
