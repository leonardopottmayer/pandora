import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { agendaKeys } from './queryKeys'
import { useAlerts, useCreateAlert } from './useAlerts'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

const alert = { id: 'al1', subjectType: 'event', subjectId: 'e1', offsetMinutes: -15, channels: null, isEnabled: true }

describe('useAlerts', () => {
  it('loads alerts for a subject', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/event/e1/alerts`, () =>
        HttpResponse.json({ success: true, data: [alert] }),
      ),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useAlerts('event', 'e1'), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateAlert', () => {
  it('invalidates the subject alert list on success', async () => {
    server.use(
      http.post(`${AGENDA_BASE}/event/e1/alerts`, () =>
        HttpResponse.json({ success: true, data: alert }),
      ),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCreateAlert('event', 'e1'), { wrapper })

    await result.current.mutateAsync({ offsetMinutes: -15 })

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: agendaKeys.alertList('event', 'e1'),
    })
  })
})
