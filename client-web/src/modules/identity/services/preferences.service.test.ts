import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { getPreferences, upsertPreferences } from './preferences.service'

const PREFERENCES = `${TEST_API_BASE}/api/v1/identity/preferences`

describe('preferences.service', () => {
  it('reads the current preferences', async () => {
    server.use(
      http.get(PREFERENCES, () =>
        HttpResponse.json({ success: true, data: { theme: 'dark', language: 'pt-BR' } }),
      ),
    )
    const prefs = await getPreferences()
    expect(prefs.theme).toBe('dark')
    expect(prefs.language).toBe('pt-BR')
  })

  it('upserts theme and language via PUT', async () => {
    let body: unknown
    server.use(
      http.put(PREFERENCES, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await upsertPreferences('light', 'en')
    expect(body).toEqual({ theme: 'light', language: 'en' })
  })
})
