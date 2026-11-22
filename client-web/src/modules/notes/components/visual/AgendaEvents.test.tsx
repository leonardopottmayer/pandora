import { useState } from 'react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { AGENDA_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { createTestQueryClient, renderWithProviders } from '@/test/utils'
import * as access from '@/modules/identity/hooks/useAccess'
import { agendaKeys } from '@/modules/agenda/hooks/queryKeys'
import { useDeleteEvent } from '@/modules/agenda/hooks/useEvents'
import { formatEventRange } from '@/modules/agenda/lib/datetime'
import { agendaEventWindow } from '../../lib/agendaEventWindow'
import { AgendaBlock } from './AgendaBlock'
import { VisualEditorContext, type VisualEditorContextValue } from './visualContext'

const context: VisualEditorContextValue = { pageId: 'page', editable: true, pagesById: new Map(), foldersById: new Map(), onOpenPage: vi.fn(), pickMedia: async () => null }
const calendar = { id: 'work', name: 'Current work calendar', archivedAt: null, isVisible: false, timeZone: 'UTC' }
const now = Date.now()
const at = (days: number) => new Date(now + days * 86_400_000).toISOString()
const event = { eventId: 'series', calendarId: 'work', originalStartsAt: at(1), startsAt: at(1), endsAt: at(1.1), title: 'First occurrence', status: 'Confirmed', isAllDay: false }
const update = vi.fn()

function renderBlock(props: Record<string, unknown> = {}, editable = false) {
  return renderWithProviders(<VisualEditorContext.Provider value={context}>
    <AgendaBlock props={{ mode: 'events', ...props }} editable={editable} update={update} />
  </VisualEditorContext.Provider>)
}

beforeAll(async () => { await i18n.changeLanguage('en') })
afterEach(() => { vi.restoreAllMocks(); update.mockClear() })

describe('Agenda upcoming events', () => {
  it('uses expanded occurrences with exact date bounds and repeated calendar query parameters', async () => {
    let params: URLSearchParams | undefined
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([calendar, { ...calendar, id: 'personal', name: 'Personal' }])),
      http.get(`${AGENDA_BASE}/events`, ({ request }) => {
        params = new URL(request.url).searchParams
        return HttpResponse.json([
          { ...event, originalStartsAt: at(2), startsAt: at(2), endsAt: at(2.1), title: 'Moved occurrence' }, event,
          { ...event, eventId: 'ongoing', startsAt: at(-1), endsAt: at(0.1), title: 'Ongoing event' },
          { ...event, eventId: 'ended', startsAt: at(-1), endsAt: at(-0.1), title: 'Ended event' },
          { ...event, eventId: 'cancelled', status: 'Cancelled', title: 'Cancelled event' },
          { ...event, eventId: 'other', calendarId: 'archived', title: 'Unavailable source' },
          { ...event, eventId: 'beyond', startsAt: at(40), endsAt: at(41), title: 'Outside window' },
          { ...event, eventId: 'boundary', startsAt: params.get('to'), endsAt: at(9), title: 'At exclusive end' },
        ])
      }),
    )
    renderBlock()
    await screen.findByText('First occurrence')
    expect(screen.getByText('Moved occurrence')).toBeInTheDocument()
    expect(screen.getByText('Ongoing event')).toBeInTheDocument()
    for (const text of ['Ended event', 'Cancelled event', 'Unavailable source', 'Outside window', 'At exclusive end']) expect(screen.queryByText(text)).not.toBeInTheDocument()
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      expect.stringContaining('Ongoing event'), expect.stringContaining('First occurrence'), expect.stringContaining('Moved occurrence'),
    ])
    expect(params?.get('from')).toBe(agendaEventWindow(7, 'America/Sao_Paulo').from)
    expect(params?.get('to')).toBe(agendaEventWindow(7, 'America/Sao_Paulo').to)
    expect(params?.getAll('calendarIds')).toEqual(['personal', 'work'])
    expect(params?.has('calendarIds[]')).toBe(false)
    expect(screen.getByText(formatEventRange(event.startsAt, event.endsAt, false, 'America/Sao_Paulo', 'en'), { normalizer: (value) => value })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open in Agenda' })).toHaveAttribute('href', '/agenda/calendar')
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(update).not.toHaveBeenCalled()
  })

  it('keeps all-day dates and the exclusive midnight end in the account zone', async () => {
    const window = agendaEventWindow(7, 'America/Sao_Paulo')
    const endsAt = new Date(new Date(window.from).getTime() + 7 * 86_400_000).toISOString()
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([calendar])),
      http.get(`${AGENDA_BASE}/events`, () => HttpResponse.json([{ ...event, title: 'All-day conference', startsAt: window.from, endsAt, isAllDay: true }])),
    )
    renderBlock({ calendarId: 'work' })
    await screen.findByText('All-day conference')
    expect(screen.getByText('All day')).toBeInTheDocument()
    expect(screen.getByText(formatEventRange(window.from, endsAt, true, 'America/Sao_Paulo', 'en'), { normalizer: (value) => value })).toBeInTheDocument()
  })

  it.each(['missing', 'archived'])('does not query events for an unavailable calendar: %s', async (calendarId) => {
    let reads = 0
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([calendar, { ...calendar, id: 'archived', archivedAt: at(-1) }])),
      http.get(`${AGENDA_BASE}/events`, () => { reads++; return HttpResponse.json([event]) }),
    )
    renderBlock({ calendarId })
    await screen.findByText('This calendar is unavailable. Choose another calendar while editing the note.')
    expect(reads).toBe(0)
  })

  it('does not send an empty calendar filter that would load all events', async () => {
    let reads = 0
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([])),
      http.get(`${AGENDA_BASE}/events`, () => { reads++; return HttpResponse.json([event]) }),
    )
    renderBlock()
    await screen.findByText('No active calendars.')
    expect(reads).toBe(0)
  })

  it.each(['agenda.events.read', 'agenda.calendars.read'])('requires %s before mounting source queries', (denied) => {
    vi.spyOn(access, 'usePermission').mockImplementation((key) => key !== denied)
    let reads = 0
    server.use(http.get(`${AGENDA_BASE}/*`, () => { reads++; return HttpResponse.json([]) }))
    renderBlock()
    expect(screen.getByText('Agenda content is unavailable with your current access.')).toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it('does not read populated owner caches in public notes', () => {
    const client = createTestQueryClient()
    client.setQueryDefaults(agendaKeys.all, { gcTime: Infinity, staleTime: Infinity })
    client.setQueryData(agendaKeys.calendarList(), [calendar])
    client.setQueryData(agendaKeys.eventList({ ...agendaEventWindow(7, 'America/Sao_Paulo'), calendarIds: ['work'] }), [event])
    let reads = 0
    server.use(http.get(`${AGENDA_BASE}/*`, () => { reads++; return HttpResponse.json([]) }))
    renderWithProviders(<VisualEditorContext.Provider value={{ ...context, publicView: true }}>
      <AgendaBlock props={{ mode: 'events', calendarId: 'work' }} editable={false} update={update} />
    </VisualEditorContext.Provider>, { queryClient: client })
    expect(screen.getByText('Private content')).toBeInTheDocument()
    expect(screen.queryByText('First occurrence')).not.toBeInTheDocument()
    expect(screen.queryByText(/Current work/)).not.toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it('persists only calendar and period, clears calendar when selecting all, and clears filters on source change', async () => {
    const queries: URLSearchParams[] = []
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([calendar])),
      http.get(`${AGENDA_BASE}/events`, ({ request }) => { queries.push(new URL(request.url).searchParams); return HttpResponse.json([]) }),
      http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json([])),
    )
    function Editable() {
      const [props, setProps] = useState<Record<string, unknown>>({ mode: 'events' })
      return <AgendaBlock props={props} editable update={(next) => { update(next); setProps(next) }} />
    }
    renderWithProviders(<VisualEditorContext.Provider value={context}><Editable /></VisualEditorContext.Provider>)
    await screen.findByText('No upcoming events in this period.')
    await userEvent.click(screen.getByRole('combobox', { name: 'Event period' }))
    await userEvent.click(await screen.findByText('Next 30 days'))
    await userEvent.click(screen.getByRole('combobox', { name: 'Event calendar' }))
    await userEvent.click(await screen.findByText('Current work calendar'))
    await waitFor(() => expect(queries.at(-1)?.get('to')).toBe(agendaEventWindow(30, 'America/Sao_Paulo').to))
    await userEvent.click(screen.getByRole('combobox', { name: 'Event calendar' }))
    await userEvent.click(await screen.findByText('All active calendars'))
    await userEvent.click(screen.getByRole('combobox', { name: 'Agenda source' }))
    await userEvent.click(await screen.findByText('Today'))
    expect(update.mock.calls.map(([value]) => value)).toEqual([
      { mode: 'events', days: 30 }, { mode: 'events', days: 30, calendarId: 'work' }, { mode: 'events', days: 30 }, { mode: 'today' },
    ])
  })

  it('hides stale occurrences on error and supports Refresh without saving results', async () => {
    let fail = false
    let title = 'First occurrence'
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([calendar])),
      http.get(`${AGENDA_BASE}/events`, () => fail ? HttpResponse.json({ success: false, errorMessage: 'Events unavailable' }, { status: 500 }) : HttpResponse.json([{ ...event, title }])),
    )
    renderBlock()
    await screen.findByText('First occurrence')
    fail = true
    await userEvent.click(screen.getByRole('button', { name: 'Refresh Agenda' }))
    await screen.findByText('Events unavailable')
    expect(screen.queryByText('First occurrence')).not.toBeInTheDocument()
    fail = false
    title = 'Updated occurrence'
    await userEvent.click(screen.getByRole('button', { name: 'Refresh Agenda' }))
    await screen.findByText('Updated occurrence')
    expect(update).not.toHaveBeenCalled()
  })

  it('reacts to normal Agenda event mutations through shared query keys', async () => {
    let deleted = false
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json([calendar])),
      http.get(`${AGENDA_BASE}/events`, () => HttpResponse.json(deleted ? [] : [event])),
      http.delete(`${AGENDA_BASE}/events/series`, () => { deleted = true; return new HttpResponse(null, { status: 204 }) }),
    )
    function DeleteInAgenda() {
      const remove = useDeleteEvent()
      return <button onClick={() => remove.mutate({ id: 'series', scope: 'all' })}>Delete in Agenda</button>
    }
    renderWithProviders(<VisualEditorContext.Provider value={context}>
      <AgendaBlock props={{ mode: 'events' }} editable={false} update={update} /><DeleteInAgenda />
    </VisualEditorContext.Provider>)
    await screen.findByText('First occurrence')
    await userEvent.click(screen.getByRole('button', { name: 'Delete in Agenda' }))
    await screen.findByText('No upcoming events in this period.')
    expect(update).not.toHaveBeenCalled()
  })
})
