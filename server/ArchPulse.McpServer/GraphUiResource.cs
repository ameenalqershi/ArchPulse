// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Server;

namespace ArchPulse.McpServer;

[McpServerResourceType]
public sealed class GraphUiResource
{
    // Experimental MCP Apps metadata and registration are isolated at this boundary.
    // No connectDomains: HTML only talks to its embedding host through MCP Apps messaging.
    #pragma warning disable MCPEXP003
    [McpServerResource(UriTemplate = "ui://archpulse/graph",
        Name = "archpulse-engineering-graph",
        MimeType = McpApps.HtmlMimeType)]
#pragma warning disable MCPEXP003
    [McpMeta("ui", """{"csp":{"connectDomains":[],"resourceDomains":[]},"prefersBorder":false}""")]
#pragma warning restore MCPEXP003
    #pragma warning restore MCPEXP003
    [Description("Interactive architecture graph UI. It only renders source-backed structured tool output from ArchPulse Graph.")]
    public static string GraphUi()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ui", "architecture-graph.html"));
}
