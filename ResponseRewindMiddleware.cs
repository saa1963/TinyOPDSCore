using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog;
using System;
using System.IO;
using System.Threading.Tasks;
using TinyOPDSCore.Data;

namespace TinyOPDSCore
{
    public static class ResponseRewindMiddlewareExtention
    {
        public static IApplicationBuilder UseResponseRewind(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ResponseRewindMiddleware>();
        }
    }
    public class ResponseRewindMiddleware
    {
        private readonly RequestDelegate next;
        private ILogger<ResponseRewindMiddleware> logger;

        public ResponseRewindMiddleware(RequestDelegate next)
        {
            var loggerFactory = new NLogLoggerFactory();
            logger = loggerFactory.CreateLogger<ResponseRewindMiddleware>();
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {

            Stream originalBody = context.Response.Body;

            try
            {
                using (var memStream = new MemoryStream())
                {
                    context.Response.Body = memStream;

                    logger.LogInformation(
                        $"Host: {context.Request.Host.Value ?? ""} " +
                        $"Method: {context.Request.Method} " +
                        $"Path: {Uri.UnescapeDataString(context.Request.Path)}");

                    await next(context);

                    memStream.Position = 0;
                    string responseBody = new StreamReader(memStream).ReadToEnd();
                    //logger.LogInformation(responseBody);

                    logger.LogInformation(
                        $"StatusCode: {context.Response.StatusCode} " +
                        $"ContentType: {context.Response.ContentType}"
                        );

                    memStream.Position = 0;
                    await memStream.CopyToAsync(originalBody);
                }

            }
            finally
            {
                context.Response.Body = originalBody;
            }

        }
    }
}
