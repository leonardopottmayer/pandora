import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useCategoryNames,
  useCreateUserCategory,
  useUpdateUserCategory,
  useSetUserCategoryActive,
} from './useCategories'
import * as categoriesService from '../services/categories.service'

vi.mock('../services/categories.service')

beforeEach(() => vi.clearAllMocks())

describe('useCategoryNames', () => {
  it('flattens system and user categories (including children) into an id → name map', async () => {
    vi.mocked(categoriesService.listSystemCategories).mockResolvedValue([
      { id: 's1', name: 'Food', children: [{ id: 's2', name: 'Restaurants', children: [] }] },
    ] as never)
    vi.mocked(categoriesService.listUserCategories).mockResolvedValue([
      { id: 'u1', name: 'Hobby', children: [] },
    ] as never)
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useCategoryNames(), { wrapper })
    await waitFor(() => expect(result.current.size).toBe(3))
    expect(result.current.get('s2')).toBe('Restaurants')
    expect(result.current.get('u1')).toBe('Hobby')
  })
})

describe('user category mutations invalidate the categories cache', () => {
  async function expectInvalidates(run: (r: { current: { mutateAsync: (v: never) => Promise<unknown> } }) => Promise<void>, hook: () => unknown) {
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(hook, { wrapper })
    await run(result as never)
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.categories() })
  }

  it('useCreateUserCategory', async () => {
    vi.mocked(categoriesService.createUserCategory).mockResolvedValue({ id: 'u' } as never)
    await expectInvalidates(async (r) => { await r.current.mutateAsync({} as never) }, () => useCreateUserCategory())
  })

  it('useUpdateUserCategory', async () => {
    vi.mocked(categoriesService.updateUserCategory).mockResolvedValue({ id: 'u' } as never)
    await expectInvalidates(async (r) => { await r.current.mutateAsync({ id: 'u', body: {} } as never) }, () => useUpdateUserCategory())
  })

  it('useSetUserCategoryActive forwards the active flag', async () => {
    vi.mocked(categoriesService.setUserCategoryActive).mockResolvedValue({ id: 'u' } as never)
    await expectInvalidates(async (r) => { await r.current.mutateAsync({ id: 'u', active: false } as never) }, () => useSetUserCategoryActive())
    expect(categoriesService.setUserCategoryActive).toHaveBeenCalledWith('u', false)
  })
})
