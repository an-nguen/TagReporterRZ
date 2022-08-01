using Hangfire.Dashboard;

namespace TagReporter.Security;

public class MyHangfireAuthFilter: IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var ctx = context.GetHttpContext();
        return ctx.User.Identity is { IsAuthenticated: true };
    }
}