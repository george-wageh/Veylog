using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Veylog.Models;

namespace Veylog.Interceptors
{
    public class SqlLoggingInterceptor : DbCommandInterceptor
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SqlLoggingInterceptor(
            IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        // =========================================================
        // Reader
        // =========================================================

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            await SaveLog(
                command,
                SqlOperation.Reader,
                eventData.Duration.TotalMilliseconds,
                true,
                null);

            return result;
        }

        // =========================================================
        // NonQuery
        // INSERT / UPDATE / DELETE
        // =========================================================

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            await SaveLog(
                command,
                SqlOperation.NonQuery,
                eventData.Duration.TotalMilliseconds,
                true,
                null);

            return result;
        }

        // =========================================================
        // Scalar
        // COUNT / MAX / MIN / SUM / etc.
        // =========================================================

        public override async ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            await SaveLog(
                command,
                SqlOperation.Scalar,
                eventData.Duration.TotalMilliseconds,
                true,
                null);

            return result;
        }

        // =========================================================
        // SQL Command Failed
        // =========================================================

        public override async Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await SaveLog(
                command,
                GetSqlOperation(eventData),
                eventData.Duration.TotalMilliseconds,
                false,
                eventData.Exception.ToString());

            await base.CommandFailedAsync(
                command,
                eventData,
                cancellationToken);
        }

        // =========================================================
        // Determine SQL Operation
        // =========================================================

        private static SqlOperation GetSqlOperation(
            CommandErrorEventData eventData)
        {
            return eventData.ExecuteMethod switch
            {
                DbCommandMethod.ExecuteReader
                    => SqlOperation.Reader,

                DbCommandMethod.ExecuteScalar
                    => SqlOperation.Scalar,


                DbCommandMethod.ExecuteNonQuery
                    => SqlOperation.NonQuery,

                _ => SqlOperation.NonQuery
            };
        }

        // =========================================================
        // Save Log
        // =========================================================

        private async Task SaveLog(
            DbCommand command,
            SqlOperation sqlOperation,
            double elapsedMilliseconds,
            bool success,
            string? exception)
        {
            try
            {
                var traceId =
                    Activity.Current?.TraceId.ToString();

                var parameters = command.Parameters
                    .Cast<DbParameter>()
                    .ToDictionary(
                        x => x.ParameterName,
                        x => x.Value?.ToString());

                using var scope =
                    _scopeFactory.CreateScope();

                var db =
                    scope.ServiceProvider
                        .GetRequiredService<LogDbContext>();

                var log = new SqlLog
                {
                    TraceId = traceId,

                    CreatedAt = DateTime.UtcNow,

                    CommandText = command.CommandText,

                    SqlOperation = sqlOperation,

                    Parameters =
                        JsonSerializer.Serialize(parameters),

                    ElapsedMilliseconds =
                        (long)Math.Round(elapsedMilliseconds),

                    IsSuccess = success,

                    Exception = exception
                };

                db.SqlLogs.Add(log);

                await db.SaveChangesAsync();
            }
            catch
            {
                // Never break the actual application
                // because SQL logging failed.
            }
        }
    }
}