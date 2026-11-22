import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { shortDate } from './models'

const SP = 'America/Sao_Paulo'
const time = (iso: string, timeZone: string) =>
  new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', timeZone })

// The device sits in Tokyo; the account zone is São Paulo.
beforeAll(() => { vi.stubEnv('TZ', 'Asia/Tokyo') })
afterAll(() => { vi.unstubAllEnvs() })

describe('shortDate', () => {
  // 23:00 Nov 13 in São Paulo — already 11:00 Nov 14 in Tokyo.
  const now = new Date('2026-11-14T02:00:00Z')

  it('shows the time for a message from the account zone today, on that clock', () => {
    const sent = '2026-11-13T20:00:00Z' // 17:00 Nov 13 in São Paulo, 05:00 Nov 14 in Tokyo
    expect(shortDate(sent, SP, now)).toBe(time(sent, SP))
    expect(shortDate(sent, SP, now)).not.toBe(time(sent, 'Asia/Tokyo'))
  })

  it('shows the date for a message from another account day, even if it is the device today', () => {
    const sent = '2026-11-14T03:30:00Z' // 00:30 Nov 14 in São Paulo (tomorrow there), 12:30 Nov 14 in Tokyo
    expect(shortDate(sent, SP, new Date('2026-11-13T15:00:00Z'))).toBe(new Date(sent).toLocaleDateString(undefined, { timeZone: SP }))
  })
})
