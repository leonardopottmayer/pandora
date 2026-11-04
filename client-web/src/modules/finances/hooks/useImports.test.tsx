import { describe, it, expect, vi, beforeEach } from 'vitest'
import { act, renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useImportFile,
  useUploadImportFile,
  useAbortImportFile,
  useRetryImportFile,
  useAllImportFiles,
  useImportRows,
} from './useImports'
import * as importsService from '../services/imports.service'
import type { ImportStatus } from '../models'

vi.mock('../services/imports.service')

beforeEach(() => vi.clearAllMocks())

describe('useImportRows', () => {
  it('fetches the final rows when processing completes instead of keeping the previous snapshot', async () => {
    vi.mocked(importsService.getImportRows)
      .mockResolvedValueOnce([{ id: 'r1', status: 'pending' }] as never)
      .mockResolvedValueOnce([{ id: 'r1', status: 'suggestion-created' }] as never)
    const { wrapper } = createHookWrapper()
    const { result, rerender } = renderHook(({ phase }: { phase: ImportStatus }) => useImportRows('imp1', phase), {
      wrapper, initialProps: { phase: 'parsing' },
    })
    await waitFor(() => expect(result.current.data?.[0].status).toBe('pending'))
    rerender({ phase: 'completed' })
    await waitFor(() => expect(result.current.data?.[0].status).toBe('suggestion-created'))
    expect(importsService.getImportRows).toHaveBeenCalledTimes(2)
  })
})

describe('useAllImportFiles', () => {
  it('refreshes active imports and stops polling once processing completes', async () => {
    vi.useFakeTimers()
    try {
      vi.mocked(importsService.listAllImportFiles)
        .mockResolvedValueOnce([{ id: 'imp1', status: 'parsing' }] as never)
        .mockResolvedValue([{ id: 'imp1', status: 'completed' }] as never)
      const { wrapper } = createHookWrapper()
      const { result, unmount } = renderHook(() => useAllImportFiles(), { wrapper })
      await act(async () => { await vi.advanceTimersByTimeAsync(10) })
      expect(result.current.data?.[0].status).toBe('parsing')
      await act(async () => { await vi.advanceTimersByTimeAsync(5000) })
      expect(result.current.data?.[0].status).toBe('completed')
      expect(importsService.listAllImportFiles).toHaveBeenCalledTimes(2)
      await act(async () => { await vi.advanceTimersByTimeAsync(10000) })
      expect(importsService.listAllImportFiles).toHaveBeenCalledTimes(2)
      unmount()
    } finally {
      vi.useRealTimers()
    }
  })
})

describe('useImportFile polling', () => {
  it('fetches the import file by id', async () => {
    vi.mocked(importsService.getImportFile).mockResolvedValue({ id: 'imp1', status: 'completed' } as never)
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useImportFile('imp1'), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.status).toBe('completed')
  })
})

describe('useUploadImportFile', () => {
  it('forwards file/accountId/cardId and invalidates the imports cache', async () => {
    vi.mocked(importsService.uploadImportFile).mockResolvedValue({ id: 'imp1' } as never)
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useUploadImportFile(), { wrapper })
    const file = new File(['x'], 'a.ofx')
    await result.current.mutateAsync({ file, accountId: 'a1' })
    expect(importsService.uploadImportFile).toHaveBeenCalledWith(file, 'a1', undefined, undefined)
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.imports() })
  })
})

describe('useAbortImportFile / useRetryImportFile', () => {
  it('abort invalidates both the list and the file detail', async () => {
    vi.mocked(importsService.abortImportFile).mockResolvedValue(undefined as never)
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useAbortImportFile(), { wrapper })
    await result.current.mutateAsync('imp1')
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.imports() })
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.import('imp1') })
  })

  it('retry invalidates both the list and the file detail', async () => {
    vi.mocked(importsService.retryImportFile).mockResolvedValue(undefined as never)
    const { client, wrapper } = createHookWrapper()
    const spy = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useRetryImportFile(), { wrapper })
    await result.current.mutateAsync('imp1')
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.imports() })
    expect(spy).toHaveBeenCalledWith({ queryKey: financeKeys.import('imp1') })
  })
})
