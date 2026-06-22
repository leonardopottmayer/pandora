import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import { useTagLinks, useCreateTag, useUpdateTag, useDeleteTag } from './useTags'
import * as tagsService from '../services/tags.service'

vi.mock('../services/tags.service')

beforeEach(() => vi.clearAllMocks())

describe('useTagLinks', () => {
  it('does not fetch with an empty id', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useTagLinks(''), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(tagsService.getTagLinks).not.toHaveBeenCalled()
  })
})

describe('tag mutations invalidate the tags cache', () => {
  async function expectInvalidates(run: (r: { current: { mutateAsync: (v: never) => Promise<unknown> } }) => Promise<void>, hook: () => unknown) {
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(hook, { wrapper })
    await run(result as never)
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.tags() })
  }

  it('useCreateTag', async () => {
    vi.mocked(tagsService.createTag).mockResolvedValue({ id: 't' } as never)
    await expectInvalidates(async (r) => { await r.current.mutateAsync({} as never) }, () => useCreateTag())
  })

  it('useUpdateTag', async () => {
    vi.mocked(tagsService.updateTag).mockResolvedValue({ id: 't' } as never)
    await expectInvalidates(async (r) => { await r.current.mutateAsync({ id: 't', body: {} } as never) }, () => useUpdateTag())
  })

  it('useDeleteTag', async () => {
    vi.mocked(tagsService.deleteTag).mockResolvedValue(undefined as never)
    await expectInvalidates(async (r) => { await r.current.mutateAsync('t' as never) }, () => useDeleteTag())
  })
})
