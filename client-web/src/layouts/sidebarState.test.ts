import { beforeEach, describe, expect, it } from 'vitest'
import { loadCollapsed, loadOpenModules, saveCollapsed, saveOpenModules } from './sidebarState'

describe('sidebarState', () => {
  beforeEach(() => localStorage.clear())

  it('starts expanded with every module open', () => {
    expect(loadCollapsed()).toBe(false)
    expect(loadOpenModules(['mail', 'files'])).toEqual(['mail', 'files'])
  })

  it('remembers being collapsed', () => {
    saveCollapsed(true)
    expect(loadCollapsed()).toBe(true)

    saveCollapsed(false)
    expect(loadCollapsed()).toBe(false)
  })

  it('remembers the closed modules, and a module added later starts open', () => {
    saveOpenModules(['mail', 'files'], ['files'])

    expect(loadOpenModules(['mail', 'files'])).toEqual(['files'])
    expect(loadOpenModules(['mail', 'files', 'radar'])).toEqual(['files', 'radar'])
  })

  it('falls back to every module open on anything it cannot read', () => {
    localStorage.setItem('pandora.sidebar_closed_modules', 'not json')
    expect(loadOpenModules(['mail'])).toEqual(['mail'])

    localStorage.setItem('pandora.sidebar_closed_modules', '{"a":1}')
    expect(loadOpenModules(['mail'])).toEqual(['mail'])
  })
})
