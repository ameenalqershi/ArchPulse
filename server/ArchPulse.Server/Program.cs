// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;

var builder = WebApplication.CreateBuilder(args);
// The initial snapshot API is intentionally loopback-only until identity/RBAC is implemented.
builder.WebHost.UseUrls("http://127.0.0.1:5127");
builder.Services.AddCors(options => options.AddPolicy("LocalStudio", p => p
    .WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
    .AllowAnyHeader().WithMethods("GET")));
var app = builder.Build();
var path = app.Configuration["ARCHPULSE_GRAPH_PATH"];
if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    throw new InvalidOperationException("ARCHPULSE_GRAPH_PATH must point to a real indexed GraphSnapshot JSON.");
var snapshot = JsonSerializer.Deserialize<GraphSnapshot>(await File.ReadAllTextAsync(path))
    ?? throw new InvalidOperationException("Snapshot JSON cannot be decoded.");
var integrity = snapshot.VerifyIntegrity();
if (integrity.Count != 0)
    throw new InvalidOperationException("Cannot serve invalid graph: " + string.Join("; ", integrity));
app.UseCors("LocalStudio");
app.MapGet("/health", () => Results.Ok(new
{
    status = "loaded",
    revision = snapshot.Revision,
    nodes = snapshot.Nodes.Count,
    edges = snapshot.Edges.Count,
    diagnostics = snapshot.Diagnostics.Count,
    note = "Loaded static snapshot, not live indexing"
}));
app.MapGet("/api/v1/graph", () => Results.Ok(snapshot));
app.MapGet("/api/v1/graph/mermaid", () => Results.Text(snapshot.ToMermaid(), "text/plain"));
app.MapGet("/api/v1/graph/nodes/{id}", (string id) =>
{
    var node = snapshot.Nodes.FirstOrDefault(n => n.Id == id);
    if (node == null) return Results.NotFound();
    return Results.Ok(new
    {
        revision = snapshot.Revision,
        node,
        incoming = snapshot.Edges.Where(e => e.To == id).ToArray(),
        outgoing = snapshot.Edges.Where(e => e.From == id).ToArray()
    });
});
app.Run();

public partial class Program;
