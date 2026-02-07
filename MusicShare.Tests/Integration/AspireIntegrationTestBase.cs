using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace MusicShare.Tests.Integration;

/// <summary>
/// Base class for integration tests using .NET Aspire.
/// Provides access to the distributed application with all services running.
/// </summary>
public abstract class AspireIntegrationTestBase : IAsyncLifetime
{
    protected DistributedApplication? App { get; private set; }

    /// <summary>
    /// Initializes the Aspire application for testing.
    /// Override this method to customize application setup.
    /// </summary>
    public virtual async ValueTask InitializeAsync()
    {
        // Create the distributed application from the AppHost
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MusicShare_AppHost>();

        // Build the application
        App = await appHost.BuildAsync();

        // Start the application
        await App.StartAsync();
    }

    /// <summary>
    /// Cleans up the Aspire application after tests complete.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets an HTTP client for the specified resource name.
    /// </summary>
    protected HttpClient GetHttpClient(string resourceName)
    {
        if (App is null)
            throw new InvalidOperationException("Application not initialized. Call InitializeAsync first.");

        return App.CreateHttpClient(resourceName);
    }
}
