// SPDX-License-Identifier: Apache-2.0
import { test, expect } from '@playwright/test';
import fs from 'node:fs';

const source = process.env.ARCHPULSE_STATIC_TEST_REPORT;
const live = source && fs.existsSync(source)
  ? JSON.parse(fs.readFileSync(source, 'utf8')) as {
      GraphProject: string; GraphRevision: string;
      Associations: Array<{ TestMethodSymbolId: string; TargetSymbolId: string }>;
    }
  : undefined;

// Optional full local API/browser integration only. Hosted CI's browser coverage
// does not claim this runs without a trusted stored solution on loopback.
test('real Roslyn test links appear inside selected Method inspector, never as graph nodes',
  async ({ page }) => {
    test.skip(!live, 'Requires an actual locally indexed ArchPulse.sln association report.');
    const first = live!.Associations[0];
    expect(first).toBeTruthy();
    await page.goto('/');
    await page.getByLabel('Stored project name').fill(live!.GraphProject);
    await page.getByLabel('Source revision').fill(live!.GraphRevision);
    await page.getByRole('button', { name: 'Open graph' }).click();
    await expect(page.getByText('Static · ' + live!.GraphRevision)).toBeVisible();
    await page.getByPlaceholder('Class, method or Symbol ID').fill(first.TargetSymbolId);
    await page.getByRole('button', { name: 'Find' }).click();
    const inspector = page.getByRole('complementary', { name: 'Selected node evidence' });
    await expect(inspector.getByRole('region', { name: 'Method tests' })).toContainText(
      'direct static test link');
    await expect(inspector.getByRole('region', { name: 'Method tests' })).toContainText(
      first.TestMethodSymbolId);
    await expect(inspector.getByRole('region', { name: 'Method tests' })).toContainText('Not run');
    await expect(inspector.getByRole('region', { name: 'Method tests' })).toContainText('Unobserved');
    // No test case node is injected into the canonical architecture SVG.
    await expect(page.locator('svg .graph-node[aria-label^="TestCase:"]')).toHaveCount(0);
  });
