import { test, expect } from '@playwright/test'

test('API is healthy', async ({ request }) => {
  const response = await request.get('http://localhost:5000/health')
  expect(response.ok()).toBeTruthy()
  expect(await response.json()).toEqual({ status: 'ok' })
})

test('app boots without errors', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(error.message))

  await page.goto('/')

  await expect(page).toHaveTitle('Settl')
  await expect(page.locator('#root')).not.toBeEmpty()
  expect(errors).toEqual([])
})
