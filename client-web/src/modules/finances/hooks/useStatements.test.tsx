import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useStatement,
  usePayStatement,
  useCloseStatement,
  useReopenStatement,
} from './useStatements'
import * as statementsService from '../services/statements.service'

vi.mock('../services/statements.service')

beforeEach(() => vi.clearAllMocks())

describe('useStatement', () => {
  it('does not fetch with an empty id', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useStatement(''), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(statementsService.getStatement).not.toHaveBeenCalled()
  })
})

describe('statement mutations invalidate statements, cards, accounts and transactions', () => {
  const affected = [
    financeKeys.statements(),
    financeKeys.cards(),
    financeKeys.accounts(),
    financeKeys.transactions(),
  ]

  async function expectInvalidatesAll(run: (r: { current: { mutateAsync: (v: never) => Promise<unknown> } }) => Promise<void>, hook: () => unknown) {
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(hook, { wrapper })
    await run(result as never)
    for (const key of affected) expect(spy).toHaveBeenCalledWith({ queryKey: key })
  }

  it('usePayStatement', async () => {
    vi.mocked(statementsService.payStatement).mockResolvedValue({ id: 's' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync({ id: 's', body: {} } as never) }, () => usePayStatement())
  })

  it('useCloseStatement', async () => {
    vi.mocked(statementsService.closeStatement).mockResolvedValue({ id: 's' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync('s' as never) }, () => useCloseStatement())
  })

  it('useReopenStatement', async () => {
    vi.mocked(statementsService.reopenStatement).mockResolvedValue({ id: 's' } as never)
    await expectInvalidatesAll(async (r) => { await r.current.mutateAsync('s' as never) }, () => useReopenStatement())
  })
})
