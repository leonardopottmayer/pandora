import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useRecurringTransaction,
  useCreateRecurringTransaction,
  useUpdateRecurringTransaction,
  useDeleteRecurringTransaction,
  useGenerateRecurringOccurrence,
  useSetRecurringPaused,
} from './useRecurringTransactions'
import * as recurringService from '../services/recurringTransactions.service'

vi.mock('../services/recurringTransactions.service')

beforeEach(() => vi.clearAllMocks())

describe('useRecurringTransaction', () => {
  it('does not fetch with an empty id', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useRecurringTransaction(''), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(recurringService.getRecurringTransaction).not.toHaveBeenCalled()
  })
})

describe('recurring mutations invalidate the recurring cache', () => {
  async function expectInvalidatesRecurring(run: (r: { current: { mutateAsync: (v: never) => Promise<unknown> } }) => Promise<void>, hook: () => unknown) {
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(hook, { wrapper })
    await run(result as never)
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.recurring() })
  }

  it('useCreateRecurringTransaction', async () => {
    vi.mocked(recurringService.createRecurringTransaction).mockResolvedValue({ id: 'r' } as never)
    await expectInvalidatesRecurring(async (r) => { await r.current.mutateAsync({} as never) }, () => useCreateRecurringTransaction())
  })

  it('useUpdateRecurringTransaction', async () => {
    vi.mocked(recurringService.updateRecurringTransaction).mockResolvedValue({ id: 'r' } as never)
    await expectInvalidatesRecurring(async (r) => { await r.current.mutateAsync({ id: 'r', body: {} } as never) }, () => useUpdateRecurringTransaction())
  })

  it('useDeleteRecurringTransaction', async () => {
    vi.mocked(recurringService.deleteRecurringTransaction).mockResolvedValue(undefined as never)
    await expectInvalidatesRecurring(async (r) => { await r.current.mutateAsync('r' as never) }, () => useDeleteRecurringTransaction())
  })

  it('useSetRecurringPaused pauses via pauseRecurringTransaction when paused=true', async () => {
    vi.mocked(recurringService.pauseRecurringTransaction).mockResolvedValue({ id: 'r' } as never)
    await expectInvalidatesRecurring(async (r) => { await r.current.mutateAsync({ id: 'r', paused: true } as never) }, () => useSetRecurringPaused())
    expect(recurringService.pauseRecurringTransaction).toHaveBeenCalledWith('r')
    expect(recurringService.resumeRecurringTransaction).not.toHaveBeenCalled()
  })

  it('useSetRecurringPaused resumes when paused=false', async () => {
    vi.mocked(recurringService.resumeRecurringTransaction).mockResolvedValue({ id: 'r' } as never)
    await expectInvalidatesRecurring(async (r) => { await r.current.mutateAsync({ id: 'r', paused: false } as never) }, () => useSetRecurringPaused())
    expect(recurringService.resumeRecurringTransaction).toHaveBeenCalledWith('r')
  })
})

describe('useGenerateRecurringOccurrence', () => {
  it('invalidates the broad set of caches an occurrence touches', async () => {
    vi.mocked(recurringService.generateRecurringTransactionOccurrence).mockResolvedValue({ id: 't' } as never)
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useGenerateRecurringOccurrence(), { wrapper })
    await result.current.mutateAsync({ id: 'r', body: {} as never })
    for (const key of [
      financeKeys.recurring(),
      financeKeys.pending(),
      financeKeys.transactions(),
      financeKeys.accounts(),
      financeKeys.cards(),
      financeKeys.statements(),
    ]) {
      expect(spy).toHaveBeenCalledWith({ queryKey: key })
    }
  })
})
