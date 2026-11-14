// SPDX-License-Identifier: Apache-2.0
import { expect, test } from '@playwright/test';

const revision = process.env.ARCHPULSE_DATAJOURNEY_REVISION;
const source = 'Entry::M:JourneyFixture.Entrypoints.ReturnRoundTrip(System.String)';
const service = 'Service::M:JourneyFixture.RelayService.Identity(System.String)';
const sink = 'Storage::M:JourneyFixture.StorageAdapter.Store(System.String)';

test('opens a real DataJourney witness and follows its cross-project return', async ({ page }) => {
  test.skip(!revision, 'Requires the indexed DataJourney fixture on the local API.');
  await page.goto('/');
  await page.getByLabel('Stored project name').fill('Journey');
  await page.getByLabel('Source revision').fill(revision!);
  await page.getByRole('button', { name: 'Open graph' }).click();
  await expect(page.getByText('Static · ' + revision)).toBeVisible();
  await page.getByPlaceholder('Class, method or Symbol ID').fill(source);
  await page.getByRole('button', { name: 'Find' }).click();

  const panel = page.getByRole('region', { name: 'DataJourney' });
  await expect(panel).toContainText('Static possible · Runtime evidence unknown');
  await expect(panel).toContainText('Some paths may be missing');
  await panel.getByText('Declared sink · 3 hops').click();
  await expect(panel.getByText('Return to', { exact: true })).toBeVisible();
  await expect(panel.getByRole('button', { name: sink, exact: true })).toBeVisible();
  await panel.getByRole('button', { name: service, exact: true }).click();
  const inspector = page.getByRole('complementary', { name: 'Selected node evidence' });
  await expect(inspector.getByRole('heading', { name: 'string RelayService.Identity(string value)' })).toBeVisible();
  await expect(panel.getByText('Declared sink · 3 hops')).toBeVisible();
});

test('removes old method-test evidence before a delayed next selection returns', async ({ page }) => {
  test.skip(!revision, 'Requires the indexed DataJourney fixture on the local API.');

  const syntheticTestId = 'SyntheticTestFixture.OnlyForSource';
  let signalServiceRequest: () => void = () => {};
  const serviceRequested = new Promise<void>(resolve => { signalServiceRequest = resolve; });
  let releaseServiceResponse: () => void = () => {};
  const serviceRelease = new Promise<void>(resolve => { releaseServiceResponse = resolve; });

  // These responses are controlled UI fixtures, not evidence for actual test coverage.
  await page.route('**/test-links?symbolId=*', async route => {
    const selected = new URL(route.request().url()).searchParams.get('symbolId');
    const mapped = selected === source;
    if (selected === service) {
      signalServiceRequest();
      await serviceRelease;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        schema: 'archpulse.method-test-selection/v1',
        project: 'Journey',
        revision,
        targetSymbolId: selected,
        mappingStatus: mapped ? 'MappedDirectStatic' : 'Unmapped',
        mappingScope: 'RoslynStaticDirectOnly',
        runnerDiscovery: 'NotRunnerDiscovered',
        executionStatus: 'Unknown',
        observedCoverage: false,
        links: mapped ? [{
          testMethodSymbolId: syntheticTestId,
          targetSymbolId: source,
          testProject: 'SyntheticTestFixture',
          framework: 'xUnit',
          relation: 'DirectInvocation',
          verification: 'StaticSourceOnly',
          executionEvidence: 'Unobserved',
          lastResult: 'Unknown',
          callSites: [{
            revision,
            relativeFile: 'SyntheticTestFixture.cs',
            line: 1,
            column: 1,
            kind: 'Static'
          }]
        }] : []
      })
    });
  });

  await page.goto('/');
  await page.getByLabel('Stored project name').fill('Journey');
  await page.getByLabel('Source revision').fill(revision!);
  await page.getByRole('button', { name: 'Open graph' }).click();
  await expect(page.getByText('Static · ' + revision)).toBeVisible();
  await page.getByPlaceholder('Class, method or Symbol ID').fill(source);
  await page.getByRole('button', { name: 'Find' }).click();

  const tests = page.getByRole('region', { name: 'Method tests' });
  await expect(tests.getByText(syntheticTestId)).toBeVisible();
  const journey = page.getByRole('region', { name: 'DataJourney' });
  await journey.getByText('Declared sink · 3 hops').click();
  await journey.getByRole('button', { name: service, exact: true }).click();
  try {
    await serviceRequested;
    await expect(tests).toContainText('Loading source-based test associations…');
    await expect(tests.getByText(syntheticTestId)).toHaveCount(0);
  } finally {
    releaseServiceResponse();
  }
  await expect(tests).toContainText('No direct static test links mapped.');
  await expect(tests.getByText(syntheticTestId)).toHaveCount(0);
});
