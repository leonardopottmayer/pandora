import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useUpdatePendingTransaction,
  useApprovePendingTransaction,
  useRejectPendingTransaction,
  useApprovePendingTransactionBatch,
  useLinkPendingTransaction,
  useCreateTransferFromPending,
} from './usePendingTransactions'
import * as pendingService from '../services/pendingTransactions.service'

vi.mock('../services/pendingTransactions.service')

beforeEach(() => vi.clearAllMocks())

const affected = [
  financeKeys.pending(),
  financeKeys.transactions(),
  financeKeys.accounts(),
  financeKeys.cards(),
  financeKeys.statements(),
  financeKeys.imports(),
]

async function expectInvalidatesAll(run: (r: { current: { mutateAsync: (v: never) => Promise<unknown> } }) => Promise<void>, hook: () => unknown) {
  const { client, wrapper } = createHookWrapper()
  const spy = vi.spyOn(client, 'invalidateQueries')
  const { result } = renderHook(hook, { wrapper })
  await run(result as never)
  for (const key of affected) expect(spy).toHaveBeenCalledWith({ queryKey: key })
}

describe('pending transaction mutations invalidate the full effect set', () => {
  it('useUpdatePendingTransaction', async () => {
    vi.mocked(pendingService.updatePendingTransaction).mockResolvedValue({ id: 'p' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync({ id: 'p', body: {} } as never) }, () => useUpdatePendingTransaction())
  })

  it('useApprovePendingTransaction', async () => {
    vi.mocked(pendingService.approvePendingTransaction).mockResolvedValue({ id: 't' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync('p' as never) }, () => useApprovePendingTransaction())
  })

  it('useRejectPendingTransaction forwards an optional reason', async () => {
    vi.mocked(pendingService.rejectPendingTransaction).mockResolvedValue({ id: 'p' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync({ id: 'p', reason: 'dup' } as never) }, () => useRejectPendingTransaction())
    expect(pendingService.rejectPendingTransaction).toHaveBeenCalledWith('p', 'dup')
  })

  it('useApprovePendingTransactionBatch', async () => {
    vi.mocked(pendingService.approvePendingTransactionBatch).mockResolvedValue(undefined as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync(['a', 'b'] as never) }, () => useApprovePendingTransactionBatch())
    expect(pendingService.approvePendingTransactionBatch).toHaveBeenCalledWith(['a', 'b'])
  })

  it('useLinkPendingTransaction', async () => {
    vi.mocked(pendingService.linkPendingTransaction).mockResolvedValue({ id: 'p' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync({ id: 'p', transactionId: 'x' } as never) }, () => useLinkPendingTransaction())
    expect(pendingService.linkPendingTransaction).toHaveBeenCalledWith('p', 'x')
  })

  it('useCreateTransferFromPending', async () => {
    vi.mocked(pendingService.createTransferFromPending).mockResolvedValue([] as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync({} as never) }, () => useCreateTransferFromPending())
  })
})
