import type { ReactNode } from 'react'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { createTestQueryClient } from '@/test/utils'
import { financeKeys } from './queryKeys'
import { useAccounts, useCreateAccount } from './useAccounts'

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

describe('useAccounts', () => {
  it('loads the account list', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json({
          success: true,
          data: [
            { id: 'a1', name: 'Conta', type: 'checking', currency: 'BRL', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null },
          ],
        }),
      ),
    )
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useAccounts(), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateAccount', () => {
  it('invalidates the accounts cache on success', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json({
          success: true,
          data: { id: 'a2', name: 'Nova', type: 'cash', currency: 'BRL', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null },
        }),
      ),
    )
    const { client, wrapper } = wrapperWith()
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useCreateAccount(), { wrapper })

    await result.current.mutateAsync({ name: 'Nova', type: 'cash', currency: 'BRL', displayOrder: 0 })

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: financeKeys.accounts() })
  })
})
