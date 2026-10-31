import { apiClient } from '@/lib/api/client'
import type {
  MailLabel,
  MailMessage,
  Mailbox,
  MailProvider,
  MessageAction,
  MessageFilter,
  MessagePage,
  MessageSummary,
} from '../models'

const BASE = '/api/v1/communications'

export interface ServerSettings {
  imapHost: string
  imapPort: number
  smtpHost: string
  smtpPort: number
}

export interface ConnectMailboxInput extends Partial<ServerSettings> {
  provider: MailProvider
  externalAccountId: string
  /** Defaults to the Google email or the IMAP username. */
  address?: string
  displayName?: string
}

export async function listMailboxes(): Promise<Mailbox[]> {
  const { data } = await apiClient.get<Mailbox[]>(`${BASE}/mailboxes`)
  return data
}

/** Tests the connection first: a refused test is an error and nothing is created. */
export async function connectMailbox(input: ConnectMailboxInput): Promise<Mailbox> {
  const { data } = await apiClient.post<Mailbox>(`${BASE}/mailboxes`, input)
  return data
}

/** New server settings (all four fields, or none) are tested first. */
export async function updateMailbox(
  id: string,
  body: { displayName: string | null } & Partial<ServerSettings>,
): Promise<Mailbox> {
  const { data } = await apiClient.patch<Mailbox>(`${BASE}/mailboxes/${id}`, body)
  return data
}

export async function deleteMailbox(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/mailboxes/${id}`)
}

/** Runs one cycle now; a `needs-reconnect` mailbox is tested first and reconnected when it passes. */
export async function syncMailbox(id: string): Promise<Mailbox> {
  const { data } = await apiClient.post<Mailbox>(`${BASE}/mailboxes/${id}/sync`)
  return data
}

export async function listLabels(mailboxId: string): Promise<MailLabel[]> {
  const { data } = await apiClient.get<MailLabel[]>(`${BASE}/mailboxes/${mailboxId}/labels`)
  return data
}

/** The label endpoints answer with the mailbox's labels as they are after the operation. */
export async function createLabel(mailboxId: string, name: string): Promise<MailLabel[]> {
  const { data } = await apiClient.post<MailLabel[]>(`${BASE}/mailboxes/${mailboxId}/labels`, { name })
  return data
}

export async function renameLabel(id: string, name: string): Promise<MailLabel[]> {
  const { data } = await apiClient.patch<MailLabel[]>(`${BASE}/labels/${id}`, { name })
  return data
}

/** An IMAP folder with messages is refused: deleting it would delete what is inside. */
export async function deleteLabel(id: string): Promise<MailLabel[]> {
  const { data } = await apiClient.delete<MailLabel[]>(`${BASE}/labels/${id}`)
  return data
}

export interface MessageActionInput {
  mailboxId: string
  messageIds: string[]
  action: MessageAction
  labelId?: string
}

/** Goes to the provider first: a failure changes nothing. */
export async function applyMessageAction(input: MessageActionInput): Promise<MessageSummary[]> {
  const { data } = await apiClient.post<MessageSummary[]>(`${BASE}/messages/actions`, input)
  return data
}

export async function listMessages(filter: MessageFilter, before?: string, limit = 50): Promise<MessagePage> {
  const { data } = await apiClient.get<MessagePage>(`${BASE}/messages`, {
    params: { ...filter, before, limit },
  })
  return data
}

/** The first open fetches the message from the server; later ones read the cache. */
export async function getMessage(id: string): Promise<MailMessage> {
  const { data } = await apiClient.get<MailMessage>(`${BASE}/messages/${id}`)
  return data
}

export function attachmentUrl(messageId: string, attachmentId: string): string {
  return `${BASE}/messages/${messageId}/attachments/${attachmentId}`
}
