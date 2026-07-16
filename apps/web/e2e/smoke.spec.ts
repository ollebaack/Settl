import { test, expect } from '@playwright/test'
import { API } from './helpers'

test('API is healthy', async ({ request }) => {
  const response = await request.get(`${API}/health`)
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
