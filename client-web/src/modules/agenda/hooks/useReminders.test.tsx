import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { agendaKeys } from './queryKeys'
import { useCreateReminder, useReminders } from './useReminders'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

describe('useReminders', () => {
  it('loads the reminder list', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () =>
        HttpResponse.json({ success: true, data: [{ id: 'r1', title: 'X', status: 'Scheduled' }] }),
      ),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useReminders(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateReminder', () => {
  it('invalidates reminders on success', async () => {
    server.use(
      http.post(`${AGENDA_BASE}/reminders`, () =>
        HttpResponse.json({ success: true, data: { id: 'r2', title: 'New', status: 'Scheduled' } }),
      ),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCreateReminder(), { wrapper })

    await result.current.mutateAsync({ title: 'New', remindAt: '2026-08-21T12:00:00Z' })

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agendaKeys.reminders() })
  })
})
