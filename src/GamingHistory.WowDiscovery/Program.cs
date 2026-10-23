namespace GamingHistory.WowDiscovery;

public static class DiscoveryProgram
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5091, listen => listen.UseHttps()));
        DiscoveryHost.Configure(builder);
        var app = builder.Build();
        await app.Services.GetRequiredService<DynamoDiscoveryStore>().InitializeAsync(CancellationToken.None);
        DiscoveryHost.Map(app);
        await app.RunAsync();
    }
}
