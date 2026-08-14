using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace CommanderPeriscope;

public static class Bootstrap
{
    public static readonly string[] SupportedCultures = ["en"];

    public static IServiceCollection AddLocalizationSupport(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services;
    }

    public static WebApplication UseLocalizationSupport(this WebApplication app)
    {
        var options = new RequestLocalizationOptions()
            .SetDefaultCulture(SupportedCultures[0])
            .AddSupportedCultures(SupportedCultures)
            .AddSupportedUICultures(SupportedCultures);

        app.UseRequestLocalization(options);
        return app;
    }
}
