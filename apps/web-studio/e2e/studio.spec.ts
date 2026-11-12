// SPDX-License-Identifier: Apache-2.0
import { test, expect } from '@playwright/test';

test('loads an exact saved graph and drills into source symbols', async ({ page }) => {
  const jsErrors: string[] = [];
  page.on('pageerror', error => jsErrors.push(error.message));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Architecture Studio/ })).toBeVisible();
  await page.getByLabel('Stored project name').fill('Billing');
  await page.getByLabel('Source revision').fill('golden-studio');
  await page.getByRole('button', { name: 'Open graph' }).click();
  await expect(page.getByText('Static · golden-studio')).toBeVisible();

  const service = page.getByRole('button', { name: 'Project: Billing.Service' });
  await expect(service).toBeVisible();
  await service.click();
  const inspector = page.getByRole('complementary', { name: 'Selected node evidence' });
  await expect(inspector.locator('dd').filter({ hasText: 'project:Billing.Service' }).first()).toBeVisible();
  await expect(inspector.getByText('golden-studio', { exact: true })).toBeVisible();

  await service.dblclick();
  await expect(page.getByRole('button', { name: 'Namespace: Billing.Service' })).toBeVisible();
  await page.getByRole('button', { name: 'Up' }).click();
  await expect(page.getByRole('button', { name: 'Project: Billing.Service' })).toBeVisible();
  await page.getByRole('button', { name: 'Zoom in' }).click();
  await expect(page.getByText('115%')).toBeVisible();

  await page.getByPlaceholder('Class, method or Symbol ID').fill('RateCalculator');
  await page.getByRole('button', { name: 'Find' }).click();
  await expect(inspector.getByText(/RateCalculator/).first()).toBeVisible();
  await page.screenshot({ path: '/root/archpulse-rebuild-20261112/backup/studio-desktop.png',
    fullPage: true });
  expect(jsErrors).toEqual([]);
});

test('rejects nonexistent revision without displaying fake graph nodes', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Stored project name').fill('Billing');
  await page.getByLabel('Source revision').fill('not-a-real-revision');
  await page.getByRole('button', { name: 'Open graph' }).click();
  await expect(page.getByRole('alert')).toContainText('No stored graph exists');
  await expect(page.getByRole('button', { name: 'Project: Billing.Service' })).toHaveCount(0);
});

test('renders interactive controls at a narrow mobile width', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByLabel('Stored project name').fill('Billing');
  await page.getByLabel('Source revision').fill('golden-studio');
  await page.getByRole('button', { name: 'Open graph' }).click();
  await expect(page.getByRole('img', { name: 'Architecture diagram: Billing' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Copy Mermaid' })).toBeVisible();
  await page.screenshot({ path: '/root/archpulse-rebuild-20261112/backup/studio-mobile.png',
    fullPage: true });
});
