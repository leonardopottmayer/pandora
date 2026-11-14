import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE, TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CalendarBindingDto, SyncConflictDto } from '../../models'
import { GoogleCalendarSection } from './GoogleCalendarSection'

const binding: CalendarBindingDto = {
  id: 'b1',
  calendarId: 'c1',
  calendarName: 'Work',
  calendarColor: '#039be5',
  externalAccountId: 'a1',
  remoteCalendarId: 'primary',
  direction: 'read-write',
  status: 'ok',
  lastSyncedAt: '2026-11-13T12:00:00Z',
  lastError: null,
}

const conflict: SyncConflictDto = {
  id: 'x1',
  subjectType: 'event',
  subjectId: 'e1',
  title: 'Reunião',
  losingSide: 'pandora',
  losingVersion: '{"summary":"Reunião (Pandora)"}',
  detectedAt: '2026-11-13T12:05:00Z',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('GoogleCalendarSection', () => {
  it('shows which way each calendar syncs and the edits a conflict discarded', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/google/bindings`, () => HttpResponse.json({ success: true, data: [binding] })),
      http.get(`${AGENDA_BASE}/google/conflicts`, () => HttpResponse.json({ success: true, data: [conflict] })),
      http.get(`${TEST_API_BASE}/api/v1.0/integrations/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<GoogleCalendarSection />)

    expect(await screen.findByText('Read and write')).toBeInTheDocument()
    expect(await screen.findByText('Reunião · Google won')).toBeInTheDocument()
    expect(screen.getByText(/Discarded: Reunião \(Pandora\)/)).toBeInTheDocument()
  })

  it('shows no conflict list when there is none', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/google/bindings`, () => HttpResponse.json({ success: true, data: [{ ...binding, direction: 'read' }] })),
      http.get(`${AGENDA_BASE}/google/conflicts`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${TEST_API_BASE}/api/v1.0/integrations/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<GoogleCalendarSection />)

    expect(await screen.findByText('Read only')).toBeInTheDocument()
    expect(screen.queryByText('Conflicts')).not.toBeInTheDocument()
  })
})
