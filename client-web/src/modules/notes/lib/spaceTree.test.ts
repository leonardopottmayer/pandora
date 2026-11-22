import { describe, expect, it } from 'vitest'
import type { FolderDto, PageSummaryDto } from '../models'
import { buildSpaceTree, computeSpaceDrop, countBelow, lastEdited, pathTo, splitTemplateTree } from './spaceTree'

const folder = (id: string, parentId: string | null = null, orderIndex = 0): FolderDto => ({
  id, spaceId: 's', parentId, name: id, icon: null, description: null, orderIndex, createdAt: '2026-01-01T00:00:00Z', updatedAt: null,
})
const page = (id: string, over: Partial<PageSummaryDto> = {}): PageSummaryDto => ({
  id, spaceId: 's', parentId: null, folderId: null, title: id, slug: id, icon: null, orderIndex: 0,
  isFavorite: false, isTemplate: false, isArchived: false, createdAt: '2026-01-01T00:00:00Z', updatedAt: null, ...over,
})

// backend/ (dotnet/ (outbox › detail), tips), arch/, loose
const tree = buildSpaceTree(
  [folder('backend'), folder('dotnet', 'backend'), folder('arch', null, 1)],
  [
    page('outbox', { folderId: 'dotnet', updatedAt: '2026-03-01T00:00:00Z' }),
    page('detail', { parentId: 'outbox', updatedAt: '2026-04-01T00:00:00Z' }),
    page('tips', { folderId: 'backend', orderIndex: 1 }),
    page('loose'),
  ],
)

describe('buildSpaceTree', () => {
  it('groups nested templates separately without losing descendants or changing the source hierarchy', () => {
    const original = buildSpaceTree([folder('work')], [
      page('normal', { folderId: 'work' }),
      page('model', { parentId: 'normal', isTemplate: true }),
      page('child', { parentId: 'model' }),
      page('loose-model', { isTemplate: true }),
    ])
    const split = splitTemplateTree(original)
    expect(split.pages[0].children.map((node) => node.id)).toEqual(['normal'])
    expect(split.pages[0].children[0].children).toEqual([])
    expect(split.templates.map((node) => node.id)).toEqual(['model', 'loose-model'])
    expect(split.templates[0].children.map((node) => node.id)).toEqual(['child'])
    expect(pathTo(original, 'model').map((node) => node.id)).toEqual(['work', 'normal'])
  })

  it('nests folders and pages, folders first', () => {
    expect(tree.map((node) => node.id)).toEqual(['backend', 'arch', 'loose'])
    expect(tree[0].children.map((node) => node.id)).toEqual(['dotnet', 'tips'])
    expect(pathTo(tree, 'detail').map((node) => node.id)).toEqual(['backend', 'dotnet', 'outbox'])
  })

  it('counts what is below and when it last changed', () => {
    expect(countBelow(tree[0])).toEqual({ folders: 1, pages: 3 })
    expect(lastEdited(tree[0])).toBe('2026-04-01T00:00:00Z')
  })
})

describe('computeSpaceDrop', () => {
  it('moves a page above and below its siblings without counting it twice', () => {
    const roots = buildSpaceTree([], [page('a'), page('b'), page('c')])
    expect(computeSpaceDrop(roots, 'a', 'c', 1, true)).toEqual([
      { kind: 'page', id: 'b', body: { parentId: null, folderId: null, orderIndex: 0 } },
      { kind: 'page', id: 'c', body: { parentId: null, folderId: null, orderIndex: 1 } },
      { kind: 'page', id: 'a', body: { parentId: null, folderId: null, orderIndex: 2 } },
    ])
    expect(computeSpaceDrop(roots, 'c', 'a', -1, true)).toEqual([
      { kind: 'page', id: 'c', body: { parentId: null, folderId: null, orderIndex: 0 } },
      { kind: 'page', id: 'a', body: { parentId: null, folderId: null, orderIndex: 1 } },
      { kind: 'page', id: 'b', body: { parentId: null, folderId: null, orderIndex: 2 } },
    ])
  })

  it('returns a nested page to the root and leaves models out of the visible sibling order', () => {
    const original = buildSpaceTree([folder('work')], [
      page('a', { folderId: 'work' }), page('model', { isTemplate: true }), page('b'),
    ])
    const visible = splitTemplateTree(original).pages
    expect(computeSpaceDrop(visible, 'a', 'b', -1, true)).toEqual([
      { kind: 'page', id: 'a', body: { parentId: null, folderId: null, orderIndex: 0 } },
      { kind: 'page', id: 'b', body: { parentId: null, folderId: null, orderIndex: 1 } },
    ])
    expect(original[1].id).toBe('model')
  })

  it('ignores a drop on itself or an item no longer present in the tree', () => {
    expect(computeSpaceDrop(tree, 'loose', 'loose', 0, false)).toEqual([])
    expect(computeSpaceDrop(tree, 'missing', 'loose', 0, false)).toEqual([])
    expect(computeSpaceDrop(tree, 'loose', 'missing', 0, false)).toEqual([])
  })

  it('drops a page onto a folder, at its end', () => {
    expect(computeSpaceDrop(tree, 'loose', 'arch', 0, false)).toEqual([
      { kind: 'page', id: 'loose', body: { parentId: null, folderId: 'arch', orderIndex: 0 } },
    ])
  })

  it('drops a page onto a page, as a sub-page', () => {
    expect(computeSpaceDrop(tree, 'loose', 'tips', 0, false)).toEqual([
      { kind: 'page', id: 'loose', body: { parentId: 'tips', folderId: null, orderIndex: 0 } },
    ])
  })

  it('reorders folders beside each other', () => {
    expect(computeSpaceDrop(tree, 'arch', 'backend', -1, true)).toEqual([
      { kind: 'folder', id: 'arch', body: { parentId: null, orderIndex: 0 } },
      { kind: 'folder', id: 'backend', body: { parentId: null, orderIndex: 1 } },
    ])
  })

  it('puts a page dropped beside a folder at the top of that level’s pages', () => {
    expect(computeSpaceDrop(tree, 'tips', 'arch', 1, true)).toEqual([
      { kind: 'page', id: 'tips', body: { parentId: null, folderId: null, orderIndex: 0 } },
      { kind: 'page', id: 'loose', body: { parentId: null, folderId: null, orderIndex: 1 } },
    ])
  })

  it('refuses a folder inside a page, or inside itself', () => {
    expect(computeSpaceDrop(tree, 'arch', 'loose', 0, false)).toBe('folderInPage')
    expect(computeSpaceDrop(tree, 'arch', 'detail', 1, true)).toBe('folderInPage')
    expect(computeSpaceDrop(tree, 'backend', 'dotnet', 0, false)).toBe('cycle')
    expect(computeSpaceDrop(tree, 'outbox', 'detail', 0, false)).toBe('cycle')
  })
})
