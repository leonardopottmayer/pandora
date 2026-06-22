import type { ReactNode } from 'react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { createTestQueryClient } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useTransaction,
  useCreateTransaction,
  useCreateTransfer,
  useUpdateTransaction,
  usePostTransaction,
  useVoidTransaction,
} from './useTransactions'
import * as transactionsService from '../services/transactions.service'

vi.mock('../services/transactions.service')

function wrapperWith(client = createTestQueryClient()) {
  return {
    client,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  }
}

beforeEach(() => vi.clearAllMocks())

describe('useTransaction', () => {
  it('is disabled (does not fetch) when no id is given', () => {
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useTransaction(null), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(transactionsService.getTransaction).not.toHaveBeenCalled()
  })

  it('fetches when an id is given', async () => {
    vi.mocked(transactionsService.getTransaction).mockResolvedValue({ id: 'x1' } as never)
    const { wrapper } = wrapperWith()
    const { result } = renderHook(() => useTransaction('x1'), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(transactionsService.getTransaction).toHaveBeenCalledWith('x1')
  })
})

describe('transaction mutations invalidate the affected caches', () => {
  const affectedKeys = [
    financeKeys.transactions(),
    financeKeys.accounts(),
    financeKeys.cards(),
    financeKeys.statements(),
  ]

  async function expectInvalidatesAll(
    runMutation: (hook: ReturnType<typeof renderHook>) => Promise<void>,
    hookFn: () => unknown,
  ) {
    const { client, wrapper } = wrapperWith()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const rendered = renderHook(hookFn, { wrapper })
    await runMutation(rendered)
    for (const key of affectedKeys) {
      expect(spy).toHaveBeenCalledWith({ queryKey: key })
    }
  }

  it('useCreateTransaction', async () => {
    vi.mocked(transactionsService.createTransaction).mockResolvedValue({ id: 'x' } as never)
    await expectInvalidatesAll(
      async ({ result }) => {
        await (result.current as ReturnType<typeof useCreateTransaction>).mutateAsync({} as never)
      },
      () => useCreateTransaction(),
    )
  })

  it('useCreateTransfer', async () => {
    vi.mocked(transactionsService.createTransfer).mockResolvedValue([] as never)
    await expectInvalidatesAll(
      async ({ result }) => {
        await (result.current as ReturnType<typeof useCreateTransfer>).mutateAsync({} as never)
      },
      () => useCreateTransfer(),
    )
  })

  it('useUpdateTransaction', async () => {
    vi.mocked(transactionsService.updateTransaction).mockResolvedValue({ id: 'x' } as never)
    await expectInvalidatesAll(
      async ({ result }) => {
        await (result.current as ReturnType<typeof useUpdateTransaction>).mutateAsync({
          id: 'x',
          body: {} as never,
        })
      },
      () => useUpdateTransaction(),
    )
  })

  it('usePostTransaction', async () => {
    vi.mocked(transactionsService.postTransaction).mockResolvedValue({ id: 'x' } as never)
    await expectInvalidatesAll(
      async ({ result }) => {
        await (result.current as ReturnType<typeof usePostTransaction>).mutateAsync('x')
      },
      () => usePostTransaction(),
    )
  })

  it('useVoidTransaction', async () => {
    vi.mocked(transactionsService.voidTransaction).mockResolvedValue({ id: 'x' } as never)
    await expectInvalidatesAll(
      async ({ result }) => {
        await (result.current as ReturnType<typeof useVoidTransaction>).mutateAsync({ id: 'x' })
      },
      () => useVoidTransaction(),
    )
  })
})
