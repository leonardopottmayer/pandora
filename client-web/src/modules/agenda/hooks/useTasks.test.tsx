import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { agendaKeys } from './queryKeys'
import { useCompleteTask, useCreateTask, useTasks } from './useTasks'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

const task = { id: 't1', listId: 'l1', title: 'X', status: 'Todo', priority: 'None', dueHasTime: false, position: 0, timeZone: 'UTC' }

describe('useTasks', () => {
  it('loads tasks for a list', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () => HttpResponse.json({ success: true, data: [task] })),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useTasks({ listId: 'l1' }), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateTask', () => {
  it('invalidates tasks on success', async () => {
    server.use(
      http.post(`${AGENDA_BASE}/tasks`, () => HttpResponse.json({ success: true, data: task })),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCreateTask(), { wrapper })

    await result.current.mutateAsync({ listId: 'l1', title: 'X' })

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agendaKeys.tasks() })
  })
})

describe('useCompleteTask', () => {
  it('invalidates tasks on success', async () => {
    server.use(
      http.post(`${AGENDA_BASE}/tasks/t1/complete`, () =>
        HttpResponse.json({ success: true, data: { ...task, status: 'Done' } }),
      ),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCompleteTask(), { wrapper })

    await result.current.mutateAsync('t1')

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agendaKeys.tasks() })
  })
})
