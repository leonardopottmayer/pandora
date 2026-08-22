import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { agendaKeys } from './queryKeys'
import { useCreateTaskList, useTaskLists } from './useTaskLists'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

const list = { id: 'l1', name: 'Inbox', isDefault: true, position: 0, archivedAt: null }

describe('useTaskLists', () => {
  it('loads task lists', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json({ success: true, data: [list] })),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useTaskLists(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateTaskList', () => {
  it('invalidates task lists on success', async () => {
    server.use(
      http.post(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json({ success: true, data: list })),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCreateTaskList(), { wrapper })

    await result.current.mutateAsync({ name: 'Inbox' })

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agendaKeys.taskLists() })
  })
})
