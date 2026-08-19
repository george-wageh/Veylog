using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Veylog.Logging;
using Veylog.Models;

namespace Veylog.Middleware
{
    public class ApiLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly VeylogOptions _options;
        private readonly ILogQueue _logQueue;

        public ApiLoggingMiddleware(
            RequestDelegate next,
            VeylogOptions options,
            ILogQueue logQueue)
        {
            _next = next;
            _options = options;
            _logQueue = logQueue;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // =========================
            // API Logging Disabled
            // =========================

            if (!_options.EnableApiLogging)
            {
                await _next(context);
                return;
            }

            // =========================
            // Skip Veylog Requests
            // =========================

            if (context.Request.Path.Value?.Contains(
                    "veylog",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                await _next(context);
                return;
            }

            // =========================
            // Skip CORS Preflight
            // =========================

            var isCorsPreflight =
                context.Request.Method.Equals(
                    "OPTIONS",
                    StringComparison.OrdinalIgnoreCase)
                &&
                context.Request.Headers.TryGetValue(
                    "Access-Control-Request-Method",
                    out var requestedMethod)
                &&
                !string.IsNullOrWhiteSpace(requestedMethod);

            if (isCorsPreflight)
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            var traceId =
                Activity.Current?.TraceId.ToString()
                ?? context.TraceIdentifier;

            string? requestBody = null;
            string? responseBody = null;
            string? exception = null;

            // =========================
            // Request Body Logging
            // =========================

            if (_options.EnableRequestLogging &&
                context.Request.ContentLength > 0 &&
                context.Request.ContentType?.Contains(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase) == true)
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
            // Response Body Logging
            // =========================

            var originalResponseBody = context.Response.Body;

            MemoryStream? responseBodyStream = null;

            if (_options.EnableResponseLogging)
            {
                responseBodyStream = new MemoryStream();

                context.Response.Body = responseBodyStream;
            }

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                exception = ex.ToString();

                // Note: the error callback (VeylogOptions.OnError) is invoked
                // from LogFlushBackgroundService once the ApiLog is actually
                // persisted, so it has a real database Id to report. We just
                // capture the exception text here to store on the log.

                throw;
            }
            finally
            {
                stopwatch.Stop();

                // =========================
                // Read Response Body
                // =========================

                if (_options.EnableResponseLogging &&
                    responseBodyStream != null)
                {
                    try
                    {
                        context.Response.Body.Seek(
                            0,
                            SeekOrigin.Begin);

                        using var reader = new StreamReader(
                            context.Response.Body,
                            Encoding.UTF8,
                            leaveOpen: true);

                        responseBody = await reader.ReadToEndAsync();

                        context.Response.Body.Seek(
                            0,
                            SeekOrigin.Begin);

                        await context.Response.Body.CopyToAsync(
                            originalResponseBody);

                        responseBody =
                            MaskSensitiveData(responseBody);
                    }
                    finally
                    {
                        context.Response.Body =
                            originalResponseBody;

                        await responseBodyStream.DisposeAsync();
                    }
                }
                else
                {
                    // Response logging is disabled.
                    // Make sure the original response stream remains intact.
                    context.Response.Body =
                        originalResponseBody;
                }

                // =========================
                // Save API Log
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
                        StatusCode = exception != null ? 500 : context.Response.StatusCode,
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        RequestBody = requestBody,
                        ResponseBody = responseBody,
                        Exception = exception
                    };

                    _logQueue.Enqueue(log);

                }
                catch
                {
                    // Never break the actual API
                    // because Veylog logging failed.
                }
            }
        }

        // =========================
        // Mask Sensitive Data
        // =========================

        private static string? MaskSensitiveData(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;

            try
            {
                using var document = JsonDocument.Parse(json);

                var dictionary =
                    JsonSerializer.Deserialize<
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
