using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Veylog
{
    public class LibraryAccessFilter : IAsyncPageFilter
    {
        private readonly VeylogTokenManager _tokenManager;

        public LibraryAccessFilter(
            VeylogTokenManager tokenManager)
        {
            _tokenManager = tokenManager;
        }

        public Task OnPageHandlerSelectionAsync(
            PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        public async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context,
            PageHandlerExecutionDelegate next)
        {
            // Authenticate Veylog session
            var result = await context.HttpContext.AuthenticateAsync(
                "VeylogScheme");

            // No Veylog session
            if (!result.Succeeded)
            {
                context.Result = new RedirectResult(
                    "/veylog/login");

                return;
            }

            // Current master token is expired/invalid
            if (!_tokenManager.IsValid())
            {
                await context.HttpContext.SignOutAsync(
                    "VeylogScheme");

                context.Result = new RedirectResult(
                    "/veylog/login");

                return;
            }

            // Get session CreatedAt
            var createdAtClaim = result.Principal
                .FindFirst("VeylogTokenCreatedAt")
                ?.Value;

            if (!long.TryParse(
                    createdAtClaim,
                    out var sessionCreatedAtTicks))
            {
                await context.HttpContext.SignOutAsync(
                    "VeylogScheme");

                context.Result = new RedirectResult(
                    "/veylog/login");

                return;
            }

            var sessionCreatedAt =
                new DateTimeOffset(sessionCreatedAtTicks, TimeSpan.Zero);

            // Check if this session belongs to the CURRENT token
            if (!_tokenManager.IsSessionValid(sessionCreatedAt))
            {
                await context.HttpContext.SignOutAsync(
                    "VeylogScheme");

                context.Result = new RedirectResult(
                    "/veylog/login");

                return;
            }

            // Everything is valid
            await next();
        }
    }
}
