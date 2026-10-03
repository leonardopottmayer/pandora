import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { AttachmentKind, AttachmentOwner } from '../models'
import * as attachmentsService from '../services/attachments.service'

export function useAttachments(owner: AttachmentOwner) {
  return useQuery({
    queryKey: financeKeys.attachmentList(owner),
    queryFn: () => attachmentsService.listAttachments(owner),
  })
}

/** Refreshes the owner's attachments and the lists that show how many each row has. */
function useInvalidateAttachments(owner: AttachmentOwner) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: financeKeys.attachmentList(owner) })
    queryClient.invalidateQueries({ queryKey: financeKeys.transactions() })
    queryClient.invalidateQueries({ queryKey: financeKeys.pending() })
  }
}

export function useUploadAttachment(owner: AttachmentOwner) {
  const invalidate = useInvalidateAttachments(owner)
  return useMutation({
    mutationFn: ({ kind, file }: { kind: AttachmentKind; file: File }) =>
      attachmentsService.uploadAttachment(owner, kind, file),
    onSuccess: invalidate,
  })
}

/** Files a queued attachment; refreshes the queue, every owner's list and the counts. */
export function useAssignAttachment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, owner }: { id: string; owner: AttachmentOwner }) =>
      attachmentsService.assignAttachment(id, owner),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: financeKeys.attachments() })
      queryClient.invalidateQueries({ queryKey: financeKeys.transactions() })
      queryClient.invalidateQueries({ queryKey: financeKeys.pending() })
    },
  })
}

export function useDeleteAttachment(owner: AttachmentOwner) {
  const invalidate = useInvalidateAttachments(owner)
  return useMutation({
    mutationFn: (id: string) => attachmentsService.deleteAttachment(id),
    onSuccess: invalidate,
  })
}
