import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { financeKeys } from './queryKeys'
import {
  useImportFile,
  useUploadImportFile,
  useAbortImportFile,
  useRetryImportFile,
} from './useImports'
import * as importsService from '../services/imports.service'

vi.mock('../services/imports.service')

beforeEach(() => vi.clearAllMocks())

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
