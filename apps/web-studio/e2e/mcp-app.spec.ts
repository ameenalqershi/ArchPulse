// SPDX-License-Identifier: Apache-2.0
import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const htmlFile = path.resolve(process.cwd(),
  '../../server/ArchPulse.McpServer/ui/architecture-graph.html');
const html = fs.readFileSync(htmlFile, 'utf8');

const goldenGraphPath = process.env.ARCHPULSE_MCP_GRAPH_JSON;
// When the native .NET CLI has produced its actual graph, use it.
// Otherwise the host simulator uses a small explicitly labeled contract fixture;
// this is never a user-facing fallback in the app itself.
function camelizeGraph(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(camelizeGraph);
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([key, entry]) =>
      [key.charAt(0).toLowerCase() + key.slice(1), camelizeGraph(entry)]));
  }
  return value;
}

const fixture = goldenGraphPath && fs.existsSync(goldenGraphPath)
  ? camelizeGraph(JSON.parse(fs.readFileSync(goldenGraphPath, 'utf8'))) as Record<string, unknown>
  : {
    project: 'TestWorkspace',
    revision: 'contract-fixture',
    nodes: [
      { symbolId: 'solution:Test', kind: 'Solution', label: 'TestWorkspace',
        parentSymbolId: null, evidence: { revision: 'contract-fixture',
          relativeFile: 'Test.sln', line: 0, column: 0, kind: 'Static' } },
      { symbolId: 'project:Payments', kind: 'Project', label: 'Payments',
        parentSymbolId: 'solution:Test', evidence: { revision: 'contract-fixture',
          relativeFile: 'Payments.csproj', line: 0, column: 0, kind: 'Static' } },
      { symbolId: 'Payments::M:Run', kind: 'Method', label: 'Run',
        parentSymbolId: 'project:Payments', evidence: { revision: 'contract-fixture',
          relativeFile: 'Payments.cs', line: 7, column: 2, kind: 'Static' } }
    ],
    edges: [],
    diagnostics: []
  };

async function mountHost(page: import('@playwright/test').Page, data: Record<string, unknown>) {
  await page.goto('about:blank');
  await page.evaluate(({ html, data }) => {
    const tracker = { names: [] as string[] };
    Object.assign(window, { __mcpHostTracker: tracker });
    window.addEventListener('message', event => {
      if (event.data?.jsonrpc !== '2.0' || !event.source) return;
      const msg = event.data;
      if (msg.method === 'ui/initialize') {
        (event.source as Window).postMessage({
          jsonrpc: '2.0', id: msg.id,
          result: { protocolVersion: '2025-06-18', capabilities: {},
            hostContext: { displayMode: 'inline' } }
        }, '*');
      } else if (msg.method === 'tools/call') {
        tracker.names.push(msg.params?.name ?? '');
        (event.source as Window).postMessage({
          jsonrpc: '2.0', id: msg.id, result:
            msg.params?.arguments?.revision === 'unknown-version'
              ? { isError: true, content: [{ type: 'text', text: 'No stored graph.' }] }
              : { isError: false, content: [{ type: 'text', text: 'Contract fixture' }],
                  structuredContent: data }
        }, '*');
      }
    });
    const iframe = document.createElement('iframe');
    iframe.setAttribute('sandbox', 'allow-scripts');
    iframe.setAttribute('style', 'border:0;width:100%;height:820px');
    iframe.srcdoc = html;
    document.body.appendChild(iframe);
  }, { html, data });
  const app = page.frameLocator('iframe');
  await expect(app.getByRole('status')).toContainText('MCP Apps host connected');
  return app;
}

test('MCP Apps host bridge renders actual tool structured content and supports drill down', async ({ page }) => {
  const app = await mountHost(page, fixture);
  await app.getByLabel('Stored project').fill(String(fixture.project));
  await app.getByLabel('Exact revision').fill(String(fixture.revision));
  await app.getByRole('button', { name: 'Load graph' }).click();
  await expect(app.getByRole('status')).toContainText('evidence-backed nodes');
  await expect(app.getByRole('img', { name: 'Architecture graph canvas' })).toBeVisible();
  const project = app.getByRole('button', { name: /Project: (Billing\.Service|Payments)/ });
  await expect(project).toBeVisible();
  await project.click();
  await expect(app.getByRole('complementary', { name: 'Source and evidence inspector' })).toContainText('Symbol ID');
  await project.dblclick();
  await expect(app.getByRole('button', { name: /Namespace: Billing\.Service|Method: Run/ })).toBeVisible();
  await app.getByRole('button', { name: 'Up' }).click();
  await app.getByRole('button', { name: 'Zoom in' }).click();
  await expect(app.locator('#zoom-label')).toContainText('115%');
  await app.getByLabel('Find a symbol').fill(goldenGraphPath ? 'RateCalculator' : 'Run');
  await app.getByRole('button', { name: 'Find' }).click();
  await expect(app.getByRole('complementary', { name: 'Source and evidence inspector' }))
    .toContainText(goldenGraphPath ? 'RateCalculator' : 'Run');
  const calls = await page.evaluate(() => (window as unknown as {
    __mcpHostTracker: { names: string[] }
  }).__mcpHostTracker.names);
  expect(calls).toEqual(['archpulse_graph_snapshot']);
});

test('MCP App rejects missing graph and does not invent edges or nodes', async ({ page }) => {
  const app = await mountHost(page, fixture);
  await app.getByLabel('Stored project').fill(String(fixture.project));
  await app.getByLabel('Exact revision').fill('unknown-version');
  await app.getByRole('button', { name: 'Load graph' }).click();
  await expect(app.getByRole('status')).toContainText('No stored graph');
  await expect(app.locator('#workspace')).toBeHidden();
});

test('MCP App is navigable on a narrow mobile iframe', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 900 });
  const app = await mountHost(page, fixture);
  await app.getByLabel('Stored project').fill(String(fixture.project));
  await app.getByLabel('Exact revision').fill(String(fixture.revision));
  await app.getByRole('button', { name: 'Load graph' }).click();
  await expect(app.getByRole('button', { name: 'Copy Mermaid' })).toBeVisible();
  await expect(app.getByRole('img', { name: 'Architecture graph canvas' })).toBeVisible();
});
