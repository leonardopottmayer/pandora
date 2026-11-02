import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import * as mediaService from '../services/media.service'
import type { MediaFilter } from '../services/media.service'

export function useMediaList(filter: MediaFilter) {
  return useQuery({
    queryKey: noteKeys.mediaList(filter),
    queryFn: () => mediaService.listMedia(filter),
    // Switching folders keeps the previous grid up until the next one arrives, instead of blinking.
    placeholderData: keepPreviousData,
  })
}

export function useMediaFolders() {
  return useQuery({ queryKey: noteKeys.mediaFolders(), queryFn: () => mediaService.listMediaFolders() })
}

/**
 * One item's bytes as an object URL, for the library's thumbnails and preview. Same cache entry as
 * the images a page embeds, so an image seen in a note is not fetched again here, nor the reverse.
 */
export function useMediaBlobUrl(path: string | null) {
  return useQuery({
    queryKey: noteKeys.mediaBlob(path ?? ''),
    queryFn: async () => URL.createObjectURL(await mediaService.fetchMediaBlob(path!)),
    enabled: !!path,
    staleTime: Infinity,
    gcTime: Infinity,
    retry: false,
  })
}

/** Every library mutation can change the items listed and the folders' counts, so all of it refreshes. */
function useInvalidateMedia() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: noteKeys.media() })
}

export function useUploadMedia() {
  const invalidate = useInvalidateMedia()
  return useMutation({
    mutationFn: ({ file, folderId }: { file: File; folderId?: string | null }) => mediaService.uploadMedia(file, folderId),
    onSuccess: invalidate,
  })
}

export function useRenameMedia() {
  const invalidate = useInvalidateMedia()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => mediaService.renameMedia(id, name),
    onSuccess: invalidate,
  })
}

export function useMoveMedia() {
  const invalidate = useInvalidateMedia()
  return useMutation({
    mutationFn: ({ id, folderId }: { id: string; folderId: string | null }) => mediaService.moveMedia(id, folderId),
    onSuccess: invalidate,
  })
}

export function useDeleteMedia() {
  const invalidate = useInvalidateMedia()
  return useMutation({ mutationFn: (id: string) => mediaService.deleteMedia(id), onSuccess: invalidate })
}

export function useCreateMediaFolder() {
  const invalidate = useInvalidateMedia()
  return useMutation({
    mutationFn: ({ name, parentId }: { name: string; parentId: string | null }) =>
      mediaService.createMediaFolder(name, parentId),
    onSuccess: invalidate,
  })
}

export function useRenameMediaFolder() {
  const invalidate = useInvalidateMedia()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => mediaService.renameMediaFolder(id, name),
    onSuccess: invalidate,
  })
}

export function useMoveMediaFolder() {
  const invalidate = useInvalidateMedia()
  return useMutation({
    mutationFn: ({ id, parentId }: { id: string; parentId: string | null }) => mediaService.moveMediaFolder(id, parentId),
    onSuccess: invalidate,
  })
}

export function useDeleteMediaFolder() {
  const invalidate = useInvalidateMedia()
  return useMutation({ mutationFn: (id: string) => mediaService.deleteMediaFolder(id), onSuccess: invalidate })
}
