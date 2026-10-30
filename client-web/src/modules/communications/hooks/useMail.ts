import { keepPreviousData, useInfiniteQuery, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchAttachmentBlob } from '@/modules/notes/services/attachments.service'
import * as mailService from '../services/communications.service'
import type { ConnectMailboxInput, ServerSettings } from '../services/communications.service'
import type { MailAttachment, MessageFilter } from '../models'

// Central query keys of the Communications module; invalidating `all` refreshes every mail screen.
export const mailKeys = {
  all: ['mail'] as const,
  mailboxes: () => [...mailKeys.all, 'mailboxes'] as const,
  labels: (mailboxId: string) => [...mailKeys.all, 'labels', mailboxId] as const,
  messages: (filter: MessageFilter) => [...mailKeys.all, 'messages', filter] as const,
  message: (id: string) => [...mailKeys.all, 'message', id] as const,
  inline: (messageId: string, attachmentId: string) => [...mailKeys.all, 'inline', messageId, attachmentId] as const,
}

/** Polls while a backfill or a cycle is running, so the progress bar moves on its own. */
export function useMailboxes() {
  return useQuery({
    queryKey: mailKeys.mailboxes(),
    queryFn: mailService.listMailboxes,
    refetchInterval: (query) =>
      query.state.data?.some((m) => m.status === 'syncing' || (m.backfillCompletedAt === null && m.status === 'active'))
        ? 15_000
        : false,
  })
}

export function useLabels(mailboxIds: string[]) {
  return useQueries({
    queries: mailboxIds.map((id) => ({ queryKey: mailKeys.labels(id), queryFn: () => mailService.listLabels(id) })),
  })
}

export function useMessages(filter: MessageFilter) {
  return useInfiniteQuery({
    queryKey: mailKeys.messages(filter),
    queryFn: ({ pageParam }) => mailService.listMessages(filter, pageParam),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    placeholderData: keepPreviousData,
  })
}

export function useMessage(id: string | null) {
  return useQuery({
    queryKey: mailKeys.message(id ?? ''),
    queryFn: () => mailService.getMessage(id!),
    enabled: id !== null,
    staleTime: Infinity,
  })
}

function readAsDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(blob)
  })
}

/**
 * The inline (`cid:`) images of an opened message, by Content-ID, as `data:` URLs. The endpoint
 * needs the Bearer token, so the iframe cannot load it; and a `blob:` URL belongs to this page's
 * origin, which the sandboxed iframe does not share — a `data:` URL carries the bytes themselves.
 */
export function useInlineImages(messageId: string | undefined, attachments: MailAttachment[] | undefined) {
  const inline = (attachments ?? []).filter((a) => a.contentId && a.contentType.startsWith('image/'))
  return useQueries({
    queries: inline.map((a) => ({
      queryKey: mailKeys.inline(messageId ?? '', a.id),
      queryFn: async () => readAsDataUrl(await fetchAttachmentBlob(mailService.attachmentUrl(messageId!, a.id))),
      enabled: messageId !== undefined,
      staleTime: Infinity,
      retry: false,
    })),
    combine: (results) => {
      const urls = new Map<string, string>()
      results.forEach((result, index) => {
        if (result.data) urls.set(inline[index].contentId!, result.data)
      })
      return urls
    },
  })
}

/** Any write to a mailbox: the prefix covers mailboxes, labels and the message lists in one shot. */
function useMailMutation<TArgs, TResult>(mutationFn: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: mailKeys.all })
    },
  })
}

export function useConnectMailbox() {
  return useMailMutation((input: ConnectMailboxInput) => mailService.connectMailbox(input))
}

export function useUpdateMailbox() {
  return useMailMutation(({ id, ...body }: { id: string; displayName: string | null } & Partial<ServerSettings>) =>
    mailService.updateMailbox(id, body),
  )
}

export function useDeleteMailbox() {
  return useMailMutation((id: string) => mailService.deleteMailbox(id))
}

export function useSyncMailbox() {
  return useMailMutation((id: string) => mailService.syncMailbox(id))
}
