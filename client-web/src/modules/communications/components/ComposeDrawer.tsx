import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Checkbox, Drawer, Form, Input, Select, Space, Spin, Upload, theme } from 'antd'
import type { UploadFile } from 'antd'
import { PaperClipOutlined } from '@ant-design/icons'
import { ApiResponseError, toErrorMessage } from '@/lib/api/envelope'
import { addressLabel, type ComposeDraft, type ComposeMode, type Mailbox, type MessageSummary } from '../models'
import { useComposeDraft, useSendMessage } from '../hooks/useMail'
import { clearDraft, loadDraft, markdownToHtml, newSendKey, quoteForReply, saveDraft } from '../lib/composeBody'

export type ComposeRequest = { mode: 'new'; mailboxId?: string } | { mode: ComposeMode; message: MessageSummary }

/** The server said the mail may have gone, or that this form was already handled. */
const UNSURE_CODES = ['Communications.SendUncertain', 'Communications.AlreadySent']

/** Writing, replying and forwarding. The drawer stays open with its content until the server has taken the message. */
export function ComposeDrawer({
  request,
  mailboxes,
  onClose,
}: {
  request: ComposeRequest
  mailboxes: Mailbox[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const original = request.mode === 'new' ? undefined : request
  const draft = useComposeDraft(original?.message.id, original?.mode)

  const title = t(`mail.compose.title.${request.mode}`)

  return (
    <Drawer open title={title} width={640} onClose={onClose} destroyOnHidden>
      {original && draft.isLoading ? (
        <Spin />
      ) : original && draft.error ? (
        <Alert type="error" showIcon message={toErrorMessage(draft.error, t('mail.compose.loadError'))} />
      ) : (
        <ComposeForm request={request} draft={draft.data} mailboxes={mailboxes} onClose={onClose} />
      )}
    </Drawer>
  )
}

interface ComposeValues {
  mailboxId: string
  to: string[]
  cc: string[]
  bcc: string[]
  subject: string
  body: string
}

function ComposeForm({
  request,
  draft,
  mailboxes,
  onClose,
}: {
  request: ComposeRequest
  draft: ComposeDraft | undefined
  mailboxes: Mailbox[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const { message: toast, modal } = App.useApp()
  const { token } = theme.useToken()
  const [form] = Form.useForm<ComposeValues>()
  const send = useSendMessage()

  const original = request.mode === 'new' ? undefined : request
  const storageId = original ? `${original.mode}.${original.message.id}` : 'new'

  const [sendKey, setSendKey] = useState(newSendKey)
  const [unsure, setUnsure] = useState<string | null>(null)
  const [preview, setPreview] = useState(false)
  const [files, setFiles] = useState<UploadFile[]>([])
  const [keptIds, setKeptIds] = useState(() => draft?.attachments.map((a) => a.id) ?? [])
  const sending = useRef(false)

  const [initialValues] = useState<Partial<ComposeValues>>(() => {
    const stored = loadDraft(storageId)
    const fromOriginal = draft && original && {
      to: draft.to.map((a) => a.address),
      cc: draft.cc.map((a) => a.address),
      subject: draft.subject,
      body: original.mode === 'forward'
        ? quoteForReply(t('mail.compose.forwarded', { from: addressLabel(draft.originalFrom), date: new Date(draft.originalDate).toLocaleString() }), draft.originalText)
        : quoteForReply(t('mail.compose.wrote', { from: addressLabel(draft.originalFrom), date: new Date(draft.originalDate).toLocaleString() }), draft.originalText),
    }
    return {
      mailboxId: original?.message.mailboxId ?? (request.mode === 'new' ? request.mailboxId : undefined) ?? mailboxes[0]?.id,
      to: [],
      cc: [],
      bcc: [],
      subject: '',
      body: '',
      ...fromOriginal,
      ...stored,
    }
  })

  const body = Form.useWatch('body', form) ?? ''

  async function doSend(values: ComposeValues) {
    // A second click can land before the button is disabled. The server's key would refuse it anyway.
    if (sending.current) return
    sending.current = true
    try {
      const result = await send.mutateAsync({
        mailboxId: values.mailboxId,
        sendKey,
        to: values.to,
        cc: values.cc ?? [],
        bcc: values.bcc ?? [],
        subject: values.subject ?? '',
        textBody: values.body ?? '',
        htmlBody: markdownToHtml(values.body ?? ''),
        replyToId: original && original.mode !== 'forward' ? original.message.id : undefined,
        forwardOfId: original?.mode === 'forward' ? original.message.id : undefined,
        keepAttachmentIds: original?.mode === 'forward' ? keptIds : [],
        files: files.map((f) => f.originFileObj as File),
      })
      clearDraft(storageId)
      if (result.copySaved) toast.success(t('mail.compose.sent'))
      else toast.warning(t('mail.compose.sentNoCopy'))
      onClose()
    } catch (err) {
      // Only a failure frees the form: after a success, a late second click must not send at all.
      sending.current = false
      if (err instanceof ApiResponseError && err.code && UNSURE_CODES.includes(err.code)) setUnsure(err.firstMessage)
      else toast.error(toErrorMessage(err, t('mail.compose.sendError')))
    }
  }

  function onFinish(values: ComposeValues) {
    if (values.subject?.trim() && values.body?.trim()) {
      void doSend(values)
      return
    }
    modal.confirm({
      title: t('mail.compose.emptyTitle'),
      content: t('mail.compose.emptyContent'),
      okText: t('mail.compose.send'),
      onOk: () => doSend(values),
    })
  }

  const recipients = (name: keyof ComposeValues, label: string, required = false) => (
    <Form.Item name={name} label={label} rules={required ? [{ required: true, type: 'array', min: 1 }] : undefined}>
      <Select mode="tags" open={false} suffixIcon={null} tokenSeparators={[',', ';', ' ']} aria-label={label} />
    </Form.Item>
  )

  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={initialValues}
      onFinish={onFinish}
      onValuesChange={(_, all) => saveDraft(storageId, all)}
    >
      <Form.Item name="mailboxId" label={t('mail.compose.from')} rules={[{ required: true }]}>
        <Select
          disabled={original !== undefined}
          options={mailboxes.map((m) => ({ value: m.id, label: m.displayName ? `${m.displayName} <${m.address}>` : m.address }))}
        />
      </Form.Item>
      {recipients('to', t('mail.compose.to'), true)}
      {recipients('cc', t('mail.compose.cc'))}
      {recipients('bcc', t('mail.compose.bcc'))}
      <Form.Item name="subject" label={t('mail.compose.subject')}>
        <Input maxLength={500} />
      </Form.Item>

      <div className="mb-1 flex justify-end">
        <Button type="link" size="small" onClick={() => setPreview((p) => !p)}>
          {preview ? t('mail.compose.edit') : t('mail.compose.preview')}
        </Button>
      </div>
      <div className={preview ? 'hidden' : undefined}>
        <Form.Item name="body" label={t('mail.compose.message')} extra={t('mail.compose.markdownHint')}>
          <Input.TextArea autoSize={{ minRows: 10, maxRows: 24 }} />
        </Form.Item>
      </div>
      {preview && (
        <div
          className="mb-4 overflow-auto rounded p-3"
          style={{ border: `1px solid ${token.colorBorderSecondary}`, maxHeight: 480 }}
          // Sanitized by markdownToHtml, with images and styles removed.
          dangerouslySetInnerHTML={{ __html: markdownToHtml(body) }}
        />
      )}

      {original?.mode === 'forward' && (draft?.attachments.length ?? 0) > 0 && (
        <Form.Item label={t('mail.compose.originalFiles')}>
          <Checkbox.Group
            value={keptIds}
            onChange={(ids) => setKeptIds(ids as string[])}
            options={draft!.attachments.map((a) => ({ value: a.id, label: a.fileName ?? a.contentType }))}
          />
        </Form.Item>
      )}

      <Form.Item>
        <Upload
          multiple
          fileList={files}
          beforeUpload={() => false}
          onChange={({ fileList }) => setFiles(fileList)}
        >
          <Button icon={<PaperClipOutlined />}>{t('mail.compose.attach')}</Button>
        </Upload>
      </Form.Item>

      {unsure && (
        <Alert
          className="mb-4"
          type="warning"
          showIcon
          message={unsure}
          action={
            <Button
              size="small"
              onClick={() => {
                // Deliberate: a new key is what lets the same content go again.
                setSendKey(newSendKey())
                setUnsure(null)
              }}
            >
              {t('mail.compose.sendAnyway')}
            </Button>
          }
        />
      )}

      <Space>
        <Button type="primary" htmlType="submit" loading={send.isPending} disabled={unsure !== null}>
          {t('mail.compose.send')}
        </Button>
        <Button onClick={onClose}>{t('mail.compose.close')}</Button>
      </Space>
    </Form>
  )
}
