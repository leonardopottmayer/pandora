import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Empty, Space, Spin, Typography, theme } from 'antd'
import { PaperClipOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { downloadAttachment } from '@/modules/notes/services/attachments.service'
import { addressLabel, shortDate, type MailAddress, type MessageSummary } from '../models'
import { attachmentUrl } from '../services/communications.service'
import { useInlineImages, useMessage, useMessages } from '../hooks/useMail'
import { mailSrcDoc, prepareMailHtml } from '../lib/prepareMailHtml'

function addresses(list: MailAddress[]): string {
  return list.map((a) => (a.name ? `${a.name} <${a.address}>` : a.address)).join(', ')
}

/** The open message, with the other messages of its conversation above it. */
export function MessageReader({
  summary,
  onSelect,
}: {
  summary: MessageSummary
  onSelect: (message: MessageSummary) => void
}) {
  const { t } = useTranslation()
  const { message: toast } = App.useApp()
  const { token } = theme.useToken()
  const [allowImages, setAllowImages] = useState(false)

  const { data: opened, isLoading, error } = useMessage(summary.id)
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
      await downloadAttachment(attachmentUrl(summary.id, attachmentId), fileName ?? 'attachment')
    } catch (err) {
      toast.error(toErrorMessage(err, t('mail.downloadError')))
    }
  }

  // Inline images the HTML shows are not listed again as files.
  const files = (opened?.attachments ?? []).filter((a) => !a.contentId || !htmlBody?.includes(`cid:${a.contentId}`))

  return (
    <div className="flex h-full flex-col">
      <div className="flex flex-col gap-1 px-4 py-3" style={{ borderBottom: `1px solid ${token.colorBorderSecondary}` }}>
        <Typography.Title level={5} className="!mb-0">
          {summary.subject || t('mail.noSubject')}
        </Typography.Title>
        <Typography.Text>{summary.from ? addresses([summary.from]) : t('mail.unknownSender')}</Typography.Text>
        {summary.to.length > 0 && (
          <Typography.Text type="secondary" className="text-xs">
            {t('mail.to')}: {addresses(summary.to)}
          </Typography.Text>
        )}
        {summary.cc.length > 0 && (
          <Typography.Text type="secondary" className="text-xs">
            {t('mail.cc')}: {addresses(summary.cc)}
          </Typography.Text>
        )}
        <Typography.Text type="secondary" className="text-xs">
          {new Date(summary.sentAt).toLocaleString()}
        </Typography.Text>

        {conversation.length > 1 && (
          <div className="mt-2 flex flex-col gap-0.5">
            <Typography.Text type="secondary" className="text-xs">
              {t('mail.conversation', { count: conversation.length })}
            </Typography.Text>
            {conversation.map((m) => (
              <Button
                key={m.id}
                size="small"
                type={m.id === summary.id ? 'primary' : 'text'}
                ghost={m.id === summary.id}
                className="!justify-between"
                onClick={() => onSelect(m)}
              >
                <span className="truncate">{addressLabel(m.from)}</span>
                <span className="text-xs">{shortDate(m.sentAt)}</span>
              </Button>
            ))}
          </div>
        )}

        {files.length > 0 && (
          <Space wrap className="mt-2">
            {files.map((a) => (
              <Button key={a.id} size="small" icon={<PaperClipOutlined />} onClick={() => download(a.id, a.fileName)}>
                {a.fileName ?? a.contentType}
              </Button>
            ))}
          </Space>
        )}
      </div>

      {prepared && prepared.blockedImages > 0 && !allowImages && (
        <Alert
          banner
          type="info"
          message={t('mail.imagesBlocked')}
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
        <Alert className="m-4" type="error" showIcon message={toErrorMessage(error, t('mail.openError'))} />
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
