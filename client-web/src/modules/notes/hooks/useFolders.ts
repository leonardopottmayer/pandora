import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import type { SaveFolderRequest } from '../models'
import * as foldersService from '../services/folders.service'

export function useFolderProperties(id: string, includeArchived: boolean, tagIds: string[]) {
  return useQuery({
    queryKey: noteKeys.folderProperties(id, includeArchived, tagIds),
    queryFn: () => foldersService.getFolderProperties(id, includeArchived, tagIds),
  })
}

/** One space's folders, flat; with no `spaceId`, every space's. */
export function useFolders(spaceId: string | null = null) {
  return useQuery({
    queryKey: noteKeys.folderList(spaceId),
    queryFn: () => foldersService.listFolders(spaceId),
  })
}

/** Folders and pages hang together: deleting a folder deletes pages, so both lists refresh. */
function useInvalidateFolders() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: noteKeys.folders() })
    queryClient.invalidateQueries({ queryKey: noteKeys.pages() })
    queryClient.invalidateQueries({ queryKey: noteKeys.spaces() })
  }
}

export function useCreateFolder() {
  const invalidate = useInvalidateFolders()
  return useMutation({ mutationFn: (body: SaveFolderRequest) => foldersService.createFolder(body), onSuccess: invalidate })
}

export function useUpdateFolder() {
  const invalidate = useInvalidateFolders()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: SaveFolderRequest }) => foldersService.updateFolder(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteFolder() {
  const invalidate = useInvalidateFolders()
  return useMutation({ mutationFn: (id: string) => foldersService.deleteFolder(id), onSuccess: invalidate })
}
