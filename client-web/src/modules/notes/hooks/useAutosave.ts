import { useCallback, useEffect, useRef, useState } from 'react'

export type SaveStatus = 'idle' | 'pending' | 'saving' | 'saved' | 'error'

interface UseAutosaveOptions<T> {
  /** Called when the debounce elapses. Rejections surface as the `error` status. */
  onSave: (value: T) => Promise<unknown>
  delayMs?: number
}

/**
 * Debounces edits and saves the latest value. `flush` forces a pending save immediately —
 * used when switching pages or leaving the screen so nothing typed is lost.
 */
export function useAutosave<T>({ onSave, delayMs = 800 }: UseAutosaveOptions<T>) {
  const [status, setStatus] = useState<SaveStatus>('idle')
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const pendingRef = useRef<T | null>(null)
  const onSaveRef = useRef(onSave)
  // Kept current after every render so timers and the unmount flush call the latest closure.
  useEffect(() => {
    onSaveRef.current = onSave
  })

  const runSave = useCallback(async () => {
    if (timerRef.current) {
      clearTimeout(timerRef.current)
      timerRef.current = null
    }
    const value = pendingRef.current
    if (value === null) return
    pendingRef.current = null

    setStatus('saving')
    try {
      await onSaveRef.current(value)
      setStatus('saved')
    } catch {
      setStatus('error')
    }
  }, [])

  const schedule = useCallback(
    (value: T) => {
      pendingRef.current = value
      setStatus('pending')
      if (timerRef.current) clearTimeout(timerRef.current)
      timerRef.current = setTimeout(() => void runSave(), delayMs)
    },
    [delayMs, runSave],
  )

  /** Saves whatever is pending right now (page switch, unmount, Ctrl+S). */
  const flush = useCallback(async () => {
    if (pendingRef.current !== null) await runSave()
  }, [runSave])

  /** Drops any pending save without writing it (the target page is gone). */
  const reset = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current)
    timerRef.current = null
    pendingRef.current = null
    setStatus('idle')
  }, [])

  // Save on unmount rather than dropping the last edits.
  useEffect(() => {
    return () => {
      if (pendingRef.current !== null) void onSaveRef.current(pendingRef.current)
      if (timerRef.current) clearTimeout(timerRef.current)
    }
  }, [])

  return { status, schedule, flush, reset }
}
