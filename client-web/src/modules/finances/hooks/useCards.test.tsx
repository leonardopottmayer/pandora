import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useCard,
  useCardNames,
  useCreateCard,
  useUpdateCard,
  useDeleteCard,
  useSetCardArchived,
} from './useCards'
import * as cardsService from '../services/cards.service'

vi.mock('../services/cards.service')

beforeEach(() => vi.clearAllMocks())

describe('useCard', () => {
  it('does not fetch with an empty id', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useCard(''), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(cardsService.getCard).not.toHaveBeenCalled()
  })
})

describe('useCardNames', () => {
  it('builds an id → name map from the card list', async () => {
    vi.mocked(cardsService.listCards).mockResolvedValue([
      { id: 'c1', name: 'Nubank' },
      { id: 'c2', name: 'Itau' },
    ] as never)
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useCardNames(), { wrapper })
    await waitFor(() => expect(result.current.size).toBe(2))
    expect(result.current.get('c1')).toBe('Nubank')
  })
})

describe('card mutations invalidate the cards cache', () => {
  async function expectInvalidatesCards(run: (r: { current: { mutateAsync: (v: never) => Promise<unknown> } }) => Promise<void>, hook: () => unknown) {
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(hook, { wrapper })
    await run(result as never)
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.cards() })
  }

  it('useCreateCard', async () => {
    vi.mocked(cardsService.createCard).mockResolvedValue({ id: 'c' } as never)
    await expectInvalidatesCards(async (r) => { await r.current.mutateAsync({} as never) }, () => useCreateCard())
  })

  it('useUpdateCard', async () => {
    vi.mocked(cardsService.updateCard).mockResolvedValue({ id: 'c' } as never)
    await expectInvalidatesCards(async (r) => { await r.current.mutateAsync({ id: 'c', body: {} } as never) }, () => useUpdateCard())
  })

  it('useDeleteCard', async () => {
    vi.mocked(cardsService.deleteCard).mockResolvedValue(undefined as never)
    await expectInvalidatesCards(async (r) => { await r.current.mutateAsync('c' as never) }, () => useDeleteCard())
  })

  it('useSetCardArchived archives via archiveCard when archived=true', async () => {
    vi.mocked(cardsService.archiveCard).mockResolvedValue({ id: 'c' } as never)
    await expectInvalidatesCards(async (r) => { await r.current.mutateAsync({ id: 'c', archived: true } as never) }, () => useSetCardArchived())
    expect(cardsService.archiveCard).toHaveBeenCalledWith('c')
    expect(cardsService.unarchiveCard).not.toHaveBeenCalled()
  })

  it('useSetCardArchived unarchives via unarchiveCard when archived=false', async () => {
    vi.mocked(cardsService.unarchiveCard).mockResolvedValue({ id: 'c' } as never)
    await expectInvalidatesCards(async (r) => { await r.current.mutateAsync({ id: 'c', archived: false } as never) }, () => useSetCardArchived())
    expect(cardsService.unarchiveCard).toHaveBeenCalledWith('c')
  })
})
