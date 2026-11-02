import { describe, expect, it } from 'vitest'
import type { MediaFolderDto } from '../models'
import { buildFolderTree, folderAndDescendants, folderPath } from './mediaFolders'

const folder = (id: string, parentId: string | null, name = id): MediaFolderDto => ({ id, parentId, name, itemCount: 0 })

const folders = [folder('trips', null, 'Trips'), folder('kyoto', 'japan', 'Kyoto'), folder('japan', 'trips', 'Japan'), folder('docs', null, 'Docs')]

describe('media folders', () => {
  it('nests by parent with siblings by name', () => {
    const tree = buildFolderTree(folders)
    expect(tree.map((n) => n.name)).toEqual(['Docs', 'Trips'])
    expect(tree[1].children[0].children[0].name).toBe('Kyoto')
  })

  it('finds a folder with everything under it', () => {
    expect([...folderAndDescendants(folders, 'trips')].sort()).toEqual(['japan', 'kyoto', 'trips'])
  })

  it('walks from the top down to a folder', () => {
    expect(folderPath(folders, 'kyoto').map((f) => f.name)).toEqual(['Trips', 'Japan', 'Kyoto'])
    expect(folderPath(folders, null)).toEqual([])
  })
})
