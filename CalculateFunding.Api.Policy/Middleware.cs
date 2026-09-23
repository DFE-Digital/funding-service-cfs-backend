using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Api.Policy
{
    public class HeaderOverrideMiddleware
    {
        private readonly RequestDelegate _next;

        public HeaderOverrideMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // To avoid adding the duplicate userid and username during a retry
            if(context.Request.Headers.TryGetValue("sfa-userid", out StringValues ids))
            {
                context.Request.Headers["sfa-userid"] = ids.FirstOrDefault().Split(',')[0];
            }
            if (context.Request.Headers.TryGetValue("sfa-username", out StringValues names))
            {
                context.Request.Headers["sfa-username"] = names.FirstOrDefault().Split(',')[0];
            }

            // Call the next middleware in the pipeline
            await _next(context);
        }
    }

    public static class HeaderOverrideMiddlewareExtensions
    {
        public static IApplicationBuilder UseHeaderOverrideMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<HeaderOverrideMiddleware>();
        }
    }
}
