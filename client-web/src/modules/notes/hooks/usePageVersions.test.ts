import { act, renderHook, waitFor } from '@testing-library/react'
import { focusManager, onlineManager } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createHookWrapper } from '@/test/utils'
import { usePageVersions } from './usePageVersions'
import * as pagesService from '../services/pages.service'

afterEach(() => {
  focusManager.setFocused(undefined)
  onlineManager.setOnline(true)
  vi.restoreAllMocks()
})

describe('history revision used by restore', () => {
  it('keeps the reviewed revision when the window regains focus or reconnects', async () => {
    let latest = 3
    const list = vi.spyOn(pagesService, 'listPageVersions').mockImplementation(async () => [{
      number: latest, createdAt: '2026-11-02T12:00:00Z', source: 'edit', restoredFrom: null,
    }])
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => usePageVersions('p1'), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    latest = 4

    await act(async () => {
      focusManager.setFocused(false)
      focusManager.setFocused(true)
      onlineManager.setOnline(false)
      onlineManager.setOnline(true)
      await new Promise((resolve) => setTimeout(resolve, 0))
    })

    expect(list).toHaveBeenCalledTimes(1)
    expect(result.current.data?.pages[0][0].number).toBe(3)
    await act(async () => { await result.current.refetch() })
    await waitFor(() => expect(result.current.data?.pages[0][0].number).toBe(4))
  })
})
