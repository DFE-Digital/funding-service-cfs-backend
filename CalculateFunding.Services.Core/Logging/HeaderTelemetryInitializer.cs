using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace CalculateFunding.Services.Core.Logging
{
    public class HeaderTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public List<string> RequestHeaders { get; set; }
        public List<string> ResponseHeaders { get; set; }

        public HeaderTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            RequestHeaders = new List<string>();
            ResponseHeaders = new List<string>();
        }

        public void Initialize(ITelemetry telemetry)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return;
            }

            if (context.Request != null)
            {
                foreach (var headerName in RequestHeaders)
                {
                    var header = context.Request.Headers[headerName].ToString();
                    if (!string.IsNullOrEmpty(header) && !telemetry.Context.Properties.ContainsKey($"request-{headerName}"))
                    {
                        telemetry.Context.Properties.Add($"request-{headerName}", header);
                    }
                }
            }

            if (context.Response != null)
            {
                foreach (var headerName in ResponseHeaders)
                {
                    var header = context.Response.Headers[headerName].ToString();
                    if (!string.IsNullOrEmpty(header) && !telemetry.Context.Properties.ContainsKey($"response-{headerName}"))
                    {
                        telemetry.Context.Properties.Add($"response-{headerName}", header);
                    }
                }
            }
        }
    }
}
