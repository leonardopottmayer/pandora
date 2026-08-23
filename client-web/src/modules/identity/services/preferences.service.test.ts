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
        HttpResponse.json({
          success: true,
          data: {
            theme: 'dark',
            language: 'pt-BR',
            timeZone: 'America/Sao_Paulo',
            weekStartsOn: 'monday',
            defaultAlertOffsetMinutes: -30,
          },
        }),
      ),
    )
    const prefs = await getPreferences()
    expect(prefs.theme).toBe('dark')
    expect(prefs.language).toBe('pt-BR')
    expect(prefs.timeZone).toBe('America/Sao_Paulo')
    expect(prefs.weekStartsOn).toBe('monday')
    expect(prefs.defaultAlertOffsetMinutes).toBe(-30)
  })

  it('upserts the full preferences object via PUT', async () => {
    let body: unknown
    server.use(
      http.put(PREFERENCES, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await upsertPreferences({
      theme: 'light',
      language: 'en',
      timeZone: 'UTC',
      weekStartsOn: 'sunday',
      defaultAlertOffsetMinutes: -15,
    })
    expect(body).toEqual({
      theme: 'light',
      language: 'en',
      timeZone: 'UTC',
      weekStartsOn: 'sunday',
      defaultAlertOffsetMinutes: -15,
    })
  })
})
