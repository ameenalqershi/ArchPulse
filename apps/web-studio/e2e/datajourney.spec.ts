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
