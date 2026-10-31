export type MailProvider = 'gmail' | 'titan' | 'imap'

export type MailboxStatus = 'active' | 'syncing' | 'needs-reconnect' | 'error'

/** Backfill progress is `messageCount` / `messageTotalEstimate`. */
export interface Mailbox {
  id: string
  provider: MailProvider
  externalAccountId: string
  address: string
  displayName: string | null
  imapHost: string | null
  imapPort: number | null
  smtpHost: string | null
  smtpPort: number | null
  status: MailboxStatus
  lastError: string | null
  lastSyncedAt: string | null
  messageCount: number
  messageTotalEstimate: number | null
  backfillCompletedAt: string | null
}

export type LabelRole = 'inbox' | 'sent' | 'drafts' | 'trash' | 'spam' | 'archive'

export interface MailLabel {
  id: string
  name: string
  kind: 'system' | 'user'
  role: LabelRole | null
  isExclusive: boolean
  total: number
  unread: number
}

export interface MailAddress {
  name: string | null
  address: string
}

export interface MessageSummary {
  id: string
  mailboxId: string
  threadKey: string
  from: MailAddress | null
  to: MailAddress[]
  cc: MailAddress[]
  subject: string
  snippet: string
  sentAt: string
  isUnread: boolean
  isStarred: boolean
  hasAttachments: boolean
  labelIds: string[]
}

/** `nextCursor` goes back as `before`; null on the last page. */
export interface MessagePage {
  items: MessageSummary[]
  nextCursor: string | null
}

export interface MailAttachment {
  id: string
  fileName: string | null
  contentType: string
  sizeBytes: number
  /** Set on inline parts the HTML points at with `cid:`. */
  contentId: string | null
}

/** The HTML is as received: `prepareMailHtml` sanitizes it before rendering. */
export interface MailMessage {
  message: MessageSummary
  rfc822MessageId: string | null
  textBody: string | null
  htmlBody: string | null
  attachments: MailAttachment[]
}

export type MessageAction =
  | 'mark-read'
  | 'mark-unread'
  | 'star'
  | 'unstar'
  | 'move'
  | 'archive'
  | 'trash'
  | 'add-label'
  | 'remove-label'

/** The server takes this many messages per request, and one mailbox per request. */
export const MAX_MESSAGES_PER_ACTION = 100

/** After these the message is no longer where it was. */
export const ACTIONS_THAT_REMOVE: MessageAction[] = ['move', 'archive', 'trash']

/** Only Gmail has labels a message can carry several of; on IMAP a label is the folder it lives in. */
export function hasMultipleLabels(provider: MailProvider): boolean {
  return provider === 'gmail'
}

/** The same rule the server applies: nothing moves into Sent or Drafts; on Gmail, of the system labels, only the inbox. */
export function canMoveTo(label: MailLabel): boolean {
  if (label.role === 'sent' || label.role === 'drafts') return false
  return label.isExclusive || label.kind === 'user' || label.role === 'inbox'
}

export type ComposeMode = 'reply' | 'reply-all' | 'forward'

/** The form for a reply or a forward. `originalText` is as written: the screen quotes it. */
export interface ComposeDraft {
  to: MailAddress[]
  cc: MailAddress[]
  subject: string
  inReplyTo: string | null
  references: string[]
  originalFrom: MailAddress | null
  originalDate: string
  originalText: string
  attachments: MailAttachment[]
}

export interface MessageFilter {
  mailboxId?: string
  labelId?: string
  unread?: boolean
  q?: string
  threadKey?: string
}

/** The Integrations provider an account of each mail provider lives at. */
export const ACCOUNT_PROVIDER: Record<MailProvider, string> = { gmail: 'google', titan: 'titan', imap: 'imap' }

/** Read and change labels — what the Gmail adapter needs (`mail.google.com` also works). */
export const GMAIL_SCOPE = 'https://www.googleapis.com/auth/gmail.modify'

/** Where Google sends the browser back to after a Gmail consent: the accounts page reopens "add" on Gmail. */
export const GMAIL_RETURN = '/mail/accounts?connect=gmail'

export function addressLabel(a: MailAddress | null): string {
  return a ? a.name || a.address : ''
}

/** Today's messages show the time, older ones the date. */
export function shortDate(iso: string): string {
  const date = new Date(iso)
  return date.toDateString() === new Date().toDateString()
    ? date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : date.toLocaleDateString()
}
