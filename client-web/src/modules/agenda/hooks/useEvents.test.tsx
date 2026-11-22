import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { agendaKeys } from './queryKeys'
import { useEvent, useUpdateEvent } from './useEvents'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

const event = {
  id: 'e1',
  calendarId: 'c1',
  title: 'Standup',
  startsAt: '2026-08-21T09:00:00Z',
  endsAt: '2026-08-21T10:00:00Z',
  isAllDay: false,
  timeZone: 'UTC',
  rrule: 'FREQ=DAILY',
  recurrenceEndsAt: null,
  status: 'Confirmed',
}

describe('useEvent', () => {
  it('loads the series row', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/events/e1`, () => HttpResponse.json({ success: true, data: event })),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useEvent('e1'), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.rrule).toBe('FREQ=DAILY')
  })
})

describe('useUpdateEvent', () => {
  it('passes scope + occurrenceStart and invalidates events', async () => {
    let seenUrl = ''
    server.use(
      http.patch(`${AGENDA_BASE}/events/e1`, ({ request }) => {
        seenUrl = request.url
        return HttpResponse.json({ success: true, data: event })
      }),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useUpdateEvent(), { wrapper })

    await result.current.mutateAsync({
      id: 'e1',
      scope: 'this',
      occurrenceStart: '2026-08-22T09:00:00Z',
      body: { title: 'Renamed' },
    })

    expect(seenUrl).toContain('scope=this')
    expect(seenUrl).toContain('occurrenceStart=')
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agendaKeys.events() })
  })
})
