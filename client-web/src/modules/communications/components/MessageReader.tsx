import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Empty, Spin, Typography, theme } from 'antd'
import { ArrowLeftOutlined, PaperClipOutlined, RollbackOutlined, SendOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { downloadBlob } from '@/lib/api/blob'
import { addressLabel, shortDate, type ComposeMode, type MailAddress, type Mailbox, type MessageAction, type MessageSummary } from '../models'
import { attachmentUrl } from '../services/communications.service'
import { useInlineImages, useMessage, useMessageAction, useMessages } from '../hooks/useMail'
import { mailSrcDoc, prepareMailHtml } from '../lib/prepareMailHtml'
import { MailActions } from './MailActions'

function addresses(list: MailAddress[]): string {
  return list.map((a) => (a.name ? `${a.name} <${a.address}>` : a.address)).join(', ')
}

/** The open message, with the other messages of its conversation above it. */
export function MessageReader({
  summary,
  mailboxes,
  onSelect,
  onActionDone,
  onCompose,
  onBack,
}: {
  summary: MessageSummary
  mailboxes: Mailbox[]
  onSelect: (message: MessageSummary) => void
  onActionDone: (action: MessageAction, ids: string[]) => void
  onCompose: (mode: ComposeMode) => void
  /** On a narrow screen the reader replaces the list, and this goes back to it. */
  onBack?: () => void
}) {
  const { t } = useTranslation()
  const { message: toast } = App.useApp()
  const { token } = theme.useToken()
  const [allowImages, setAllowImages] = useState(false)

  const { data: opened, isLoading, error } = useMessage(summary.id)

  // Opening reads the message. Only once per open, and a failure is silent: it must not get in the way of reading,
  // and the mark stays as the provider has it.
  const { mutate: applyAction } = useMessageAction()
  const markedRead = useRef(false)
  const hasBody = opened !== undefined
  const { id: messageId, mailboxId, isUnread } = summary
  useEffect(() => {
    if (!hasBody || !isUnread || markedRead.current) return
    markedRead.current = true
    applyAction({ mailboxId, messageIds: [messageId], action: 'mark-read' })
  }, [hasBody, isUnread, mailboxId, messageId, applyAction])

  const thread = useMessages({ mailboxId: summary.mailboxId, threadKey: summary.threadKey })
  const conversation = thread.data?.pages.flatMap((p) => p.items) ?? []
  const inlineImages = useInlineImages(opened?.message.id, opened?.attachments)

  const htmlBody = opened?.htmlBody
  const prepared = useMemo(
    () => (htmlBody ? prepareMailHtml(htmlBody, { allowImages, inlineImages }) : null),
    [htmlBody, allowImages, inlineImages],
  )

  async function download(attachmentId: string, fileName: string | null) {
    try {
      await downloadBlob(attachmentUrl(summary.id, attachmentId), fileName ?? 'attachment')
    } catch (err) {
      toast.error(toErrorMessage(err, t('mail.downloadError')))
    }
  }

  // Inline images the HTML shows are not listed again as files.
  const files = (opened?.attachments ?? []).filter((a) => !a.contentId || !htmlBody?.includes(`cid:${a.contentId}`))

  const divider = `1px solid ${token.colorBorderSecondary}`
  const sender = summary.from ? summary.from.name || summary.from.address : t('mail.unknownSender')
  const recipients = [...summary.to, ...summary.cc]

  return (
    <div className="flex h-full flex-col">
      <div className="flex flex-wrap items-center gap-1 px-3 py-2" style={{ borderBottom: divider }}>
        {onBack && (
          <Button type="text" icon={<ArrowLeftOutlined />} aria-label={t('mail.back')} onClick={onBack} />
        )}
        <Button size="small" icon={<RollbackOutlined />} onClick={() => onCompose('reply')}>
          {t('mail.compose.reply')}
        </Button>
        <Button size="small" onClick={() => onCompose('reply-all')}>
          {t('mail.compose.replyAll')}
        </Button>
        <Button size="small" icon={<SendOutlined />} onClick={() => onCompose('forward')}>
          {t('mail.compose.forward')}
        </Button>
        <span className="ms-auto">
          <MailActions messages={[summary]} mailboxes={mailboxes} onDone={onActionDone} />
        </span>
      </div>

      <div className="flex flex-col gap-3 px-5 pb-3 pt-4" style={{ borderBottom: divider }}>
        <Typography.Title level={4} className="!mb-0 break-words">
          {summary.subject || t('mail.noSubject')}
        </Typography.Title>

        <div className="flex items-start gap-3">
          <span
            aria-hidden
            className="flex size-9 shrink-0 items-center justify-center rounded-full text-sm font-semibold uppercase"
            style={{ background: token.colorFillSecondary, color: token.colorTextSecondary }}
          >
            {sender.trim().charAt(0)}
          </span>
          <div className="flex min-w-0 flex-1 flex-col">
            <span className="flex flex-wrap items-baseline gap-x-2">
              <Typography.Text strong>{sender}</Typography.Text>
              {summary.from?.name && (
                <Typography.Text type="secondary" className="text-xs">
                  {summary.from.address}
                </Typography.Text>
              )}
            </span>
            {recipients.length > 0 && (
              <details className="text-xs" style={{ color: token.colorTextSecondary }}>
                <summary className="w-fit cursor-pointer select-none truncate">
                  {t('mail.toLine', { names: recipients.map(addressLabel).join(', ') })}
                </summary>
                <div className="mt-1 flex flex-col gap-0.5">
                  {summary.to.length > 0 && (
                    <span>
                      {t('mail.to')}: {addresses(summary.to)}
                    </span>
                  )}
                  {summary.cc.length > 0 && (
                    <span>
                      {t('mail.cc')}: {addresses(summary.cc)}
                    </span>
                  )}
                  <span>{new Date(summary.sentAt).toLocaleString()}</span>
                </div>
              </details>
            )}
          </div>
          <Typography.Text type="secondary" className="shrink-0 text-xs tabular-nums">
            {shortDate(summary.sentAt)}
          </Typography.Text>
        </div>

        {files.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {files.map((a) => (
              <button
                key={a.id}
                type="button"
                onClick={() => download(a.id, a.fileName)}
                className="flex max-w-60 cursor-pointer items-center gap-2 rounded-lg px-3 py-1.5 text-left text-sm"
                style={{ border: divider, background: token.colorFillQuaternary, color: token.colorText }}
              >
                <PaperClipOutlined style={{ color: token.colorTextSecondary }} />
                <span className="truncate">{a.fileName ?? a.contentType}</span>
              </button>
            ))}
          </div>
        )}

        {conversation.length > 1 && (
          <details>
            <summary className="w-fit cursor-pointer select-none text-xs" style={{ color: token.colorTextSecondary }}>
              {t('mail.conversation', { count: conversation.length })}
            </summary>
            <div className="mt-2 flex flex-col overflow-hidden rounded-lg" style={{ border: divider }}>
              {conversation.map((m, index) => (
                <button
                  key={m.id}
                  type="button"
                  onClick={() => onSelect(m)}
                  className="flex cursor-pointer items-center justify-between gap-3 px-3 py-1.5 text-left text-sm"
                  style={{
                    background: m.id === summary.id ? token.controlItemBgActive : 'transparent',
                    borderTop: index === 0 ? undefined : divider,
                    color: token.colorText,
                  }}
                >
                  <span className="truncate" style={{ fontWeight: m.isUnread ? 600 : 400 }}>
                    {addressLabel(m.from)}
                  </span>
                  <span className="shrink-0 text-xs tabular-nums" style={{ color: token.colorTextTertiary }}>
                    {shortDate(m.sentAt)}
                  </span>
                </button>
              ))}
            </div>
          </details>
        )}
      </div>

      {prepared && prepared.blockedImages > 0 && !allowImages && (
        <Alert
          banner
          type="info"
          title={t('mail.imagesBlocked')}
          action={
            <Button size="small" onClick={() => setAllowImages(true)}>
              {t('mail.showImages')}
            </Button>
          }
        />
      )}

      {isLoading ? (
        <Spin className="m-6" />
      ) : error ? (
        <Alert className="m-4" type="error" showIcon title={toErrorMessage(error, t('mail.openError'))} />
      ) : prepared ? (
        <iframe
          title={summary.subject}
          // No allow-scripts and no allow-same-origin: the message cannot run code or reach this page.
          sandbox="allow-popups allow-popups-to-escape-sandbox"
          referrerPolicy="no-referrer"
          srcDoc={mailSrcDoc(prepared.html, allowImages)}
          className="w-full flex-1 border-0 bg-white"
        />
      ) : opened?.textBody ? (
        <pre className="m-0 flex-1 overflow-auto whitespace-pre-wrap break-words p-4 font-sans text-sm">
          {opened.textBody}
        </pre>
      ) : (
        <Empty className="mt-10" description={t('mail.emptyBody')} />
      )}
    </div>
  )
}
