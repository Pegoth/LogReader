using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogReader;

internal static class Program
{
    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var host = Host.CreateDefaultBuilder(args)
                       .ConfigureServices((context, services) =>
                        {
                            services.AddSingleton<MainForm>()
                                    .AddWindowsFormsBlazorWebView();

                            if (context.HostingEnvironment.IsDevelopment())
                                services.AddBlazorWebViewDeveloperTools();
                        })
                       .Build();

        // Get the main form before run to make sure it is initialized from the STA thread
        var form = host.Services.GetRequiredService<MainForm>();

        // Run both the host and the application thread
        var cts = new CancellationTokenSource();
        host.RunAsync(cts.Token);
        Application.Run(form);
        cts.Cancel();
    }
}