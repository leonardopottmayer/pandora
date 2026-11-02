import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import type { SaveSpaceRequest } from '../models'
import * as spacesService from '../services/spaces.service'

export function useSpaces(includeArchived = false) {
  return useQuery({
    queryKey: noteKeys.spaceList(includeArchived),
    queryFn: () => spacesService.listSpaces(includeArchived),
  })
}

function useInvalidateSpaces() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: noteKeys.spaces() })
}

export function useCreateSpace() {
  const invalidate = useInvalidateSpaces()
  return useMutation({ mutationFn: (body: SaveSpaceRequest) => spacesService.createSpace(body), onSuccess: invalidate })
}

export function useUpdateSpace() {
  const invalidate = useInvalidateSpaces()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: SaveSpaceRequest }) => spacesService.updateSpace(id, body),
    onSuccess: invalidate,
  })
}

export function useSetDefaultSpace() {
  const invalidate = useInvalidateSpaces()
  return useMutation({ mutationFn: (id: string) => spacesService.setDefaultSpace(id), onSuccess: invalidate })
}

export function useSetSpaceArchived() {
  const invalidate = useInvalidateSpaces()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) => spacesService.setSpaceArchived(id, archived),
    onSuccess: invalidate,
  })
}

export function useDeleteSpace() {
  const invalidate = useInvalidateSpaces()
  return useMutation({ mutationFn: (id: string) => spacesService.deleteSpace(id), onSuccess: invalidate })
}
