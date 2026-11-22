import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useAutosave } from './useAutosave'

afterEach(() => vi.useRealTimers())

describe('autosave before history', () => {
  it('waits for an in-flight save and serializes the latest draft before completing flush', async () => {
    vi.useFakeTimers()
    let finish!: () => void
    const onSave = vi.fn().mockImplementationOnce(() => new Promise<void>((resolve) => { finish = resolve })).mockResolvedValue(undefined)
    const { result } = renderHook(() => useAutosave<string>({ onSave }))
    act(() => result.current.schedule('first'))
    await act(() => vi.advanceTimersByTimeAsync(800))
    act(() => result.current.schedule('second'))
    let flush!: Promise<void>
    act(() => { flush = result.current.flush() })
    expect(onSave).toHaveBeenCalledTimes(1)
    await act(async () => { finish(); await flush })
    expect(onSave.mock.calls.map(([value]) => value)).toEqual(['first', 'second'])
    expect(result.current.status).toBe('saved')
  })

  it('rejects a failed flush and keeps the draft for a successful retry', async () => {
    const onSave = vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValue(undefined)
    const { result } = renderHook(() => useAutosave<string>({ onSave }))
    act(() => result.current.schedule('unsaved text'))
    await act(async () => { await expect(result.current.flush()).rejects.toThrow('offline') })
    expect(result.current.status).toBe('error')
    await act(async () => { await result.current.flush() })
    expect(onSave.mock.calls.map(([value]) => value)).toEqual(['unsaved text', 'unsaved text'])
    expect(result.current.status).toBe('saved')
  })
})
