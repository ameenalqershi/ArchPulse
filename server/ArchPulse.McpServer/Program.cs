// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.McpServer;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

// Deliberately no dynamic bind setting: only the authorized user's own
// localhost can reach the unauthenticated experimental MCP HTTP endpoint.
builder.WebHost.UseUrls("http://127.0.0.1:5231");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 512 * 1024);

builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var storePath = config["ARCHPULSE_GRAPH_STORE"];
    if (string.IsNullOrWhiteSpace(storePath))
        throw new InvalidOperationException("ARCHPULSE_GRAPH_STORE must name an existing trusted local graph store.");
    if (!Directory.Exists(storePath))
        throw new DirectoryNotFoundException("Configured local graph store does not exist.");
    return new GraphSnapshotStore(storePath);
});

builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var storePath = config["ARCHPULSE_GRAPH_STORE"];
    if (string.IsNullOrWhiteSpace(storePath) || !Directory.Exists(storePath))
        throw new InvalidOperationException("ARCHPULSE_GRAPH_STORE must be an existing trusted local store.");
    return new StaticTestAssociationStore(storePath);
});

builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var storePath = config["ARCHPULSE_GRAPH_STORE"];
    if (string.IsNullOrWhiteSpace(storePath) || !Directory.Exists(storePath))
        throw new InvalidOperationException("ARCHPULSE_GRAPH_STORE must be an existing trusted local store.");
    return new FeatureCatalogStore(storePath);
});

builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "archpulse-local-graph",
            Version = "0.1.0",
            Title = "ArchPulse Graph — Experimental Local MCP App",
            Description = "Source-evidenced C# architecture graph; read-only, localhost-only."
        };
        options.Capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability(),
            Resources = new ResourcesCapability()
        };
    })
    .WithHttpTransport()
    .WithTools<GraphTools>()
    .WithResources<GraphUiResource>()
// The upstream MCP Apps extension is still experimental; isolate this opt-in
// warning to its single registration rather than disabling compiler/security audits.
#pragma warning disable MCPEXP003
    .WithMcpApps();
#pragma warning restore MCPEXP003

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new
{
    Service = "ArchPulse.LocalMcpApps",
    Transport = "streamable-http",
    PublicDeploymentReady = false,
    Authorization = "none-localhost-only"
}));
app.MapMcp("/mcp");
app.Run();

public partial class Program { }
