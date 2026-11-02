import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { graphPreferencesKey, useGraphPreferences } from './useGraphPreferences'

beforeEach(() => localStorage.clear())

describe('graph preferences', () => {
  it('starts off and remembers mode and families after remounting', () => {
    const first = renderHook(() => useGraphPreferences('user-a'))
    expect(first.result.current.preferences).toEqual({ showGroups: false, families: [] })
    const saved = { showGroups: true, families: [{ id: 'work', name: 'Work', tagIds: ['t1'], color: '#1677ff' }] }
    act(() => first.result.current.update(saved))
    first.unmount()
    const second = renderHook(() => useGraphPreferences('user-a'))
    expect(second.result.current.preferences).toEqual(saved)
    act(() => second.result.current.update({ ...saved, showGroups: false }))
    expect(JSON.parse(localStorage.getItem(graphPreferencesKey('user-a'))!).showGroups).toBe(false)
  })

  it('does not carry preferences into another account or overwrite them while loading auth', () => {
    localStorage.setItem(graphPreferencesKey('a'), JSON.stringify({ showGroups: true, families: [] }))
    const hook = renderHook(({ id }) => useGraphPreferences(id), { initialProps: { id: null as string | null } })
    hook.rerender({ id: 'a' })
    expect(hook.result.current.preferences.showGroups).toBe(true)
    hook.rerender({ id: 'b' })
    expect(hook.result.current.preferences.showGroups).toBe(false)
    hook.rerender({ id: 'a' })
    expect(hook.result.current.preferences.showGroups).toBe(true)
  })

  it.each(['{broken', '{"showGroups":true,"families":[{}]}'])('falls back for invalid stored data: %s', (value) => {
    localStorage.setItem(graphPreferencesKey('a'), value)
    const hook = renderHook(() => useGraphPreferences('a'))
    expect(hook.result.current.preferences.showGroups).toBe(false)
  })

  it('continues working when the browser blocks storage', () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked') })
    try {
      const hook = renderHook(() => useGraphPreferences('a'))
      act(() => hook.result.current.update({ showGroups: true, families: [] }))
      expect(hook.result.current.preferences.showGroups).toBe(true)
    } finally { storage.mockRestore() }
  })
})
