import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { agendaKeys } from './queryKeys'
import { useCalendars, useCreateCalendar } from './useCalendars'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

const calendar = {
  id: 'c1',
  name: 'Work',
  color: null,
  isDefault: true,
  isVisible: true,
  timeZone: 'UTC',
  origin: 'Local',
  archivedAt: null,
}

describe('useCalendars', () => {
  it('loads calendars', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () =>
        HttpResponse.json({ success: true, data: [calendar] }),
      ),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useCalendars(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateCalendar', () => {
  it('invalidates calendars on success', async () => {
    server.use(
      http.post(`${AGENDA_BASE}/calendars`, () =>
        HttpResponse.json({ success: true, data: calendar }),
      ),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCreateCalendar(), { wrapper })

    await result.current.mutateAsync({ name: 'Work' })

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agendaKeys.calendars() })
  })
})
