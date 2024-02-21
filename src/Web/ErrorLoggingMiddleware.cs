using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Devlooped.SponsorLink;

public class ErrorLoggingMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            var logger = context.GetLogger<ErrorLoggingMiddleware>();
            logger.LogError(e, "Exception: {Exception}", e.ToString());
            throw;
        }
    }
}
