import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input, Spin, Table, Tabs, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { AudioOutlined, PlusOutlined, RobotOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { onAccent } from '@/app/theme'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import type { Invocation, InvocationStatus } from '../models'
import {
  useAssistantProfile,
  useCancelInvocation,
  useConfirmInvocation,
  useInterpret,
  useInterpretAudio,
  useInvocations,
} from '../hooks/useAssistant'
import { toWav, useVoiceRecorder, voiceSupported } from '../hooks/useVoiceRecorder'
import { usePreferences } from '@/modules/identity/context/preferences-context'

const STATUS_COLOR: Record<InvocationStatus, string> = {
  executed: 'green',
  failed: 'red',
  clarification: 'blue',
  rejected: 'volcano',
  'provider-error': 'orange',
  'pending-confirmation': 'gold',
  cancelled: 'default',
  expired: 'default',
}

// A reply that did what was asked, or asks something back, reads fine as text alone.
const QUIET_STATUSES: InvocationStatus[] = ['executed', 'clarification']

const EXAMPLES = ['reminder', 'agenda', 'spending', 'notes'] as const

const HISTORY_TAB = 'history'
const CHAT_TAB = 'chat'

function StatusTag({ status }: { status: InvocationStatus }) {
  const { t } = useTranslation()
  return <Tag color={STATUS_COLOR[status]}>{t(`assistant.bar.status.${status}`)}</Tag>
}

/** What the model did and what it cost, for when a reply needs explaining. */
function InvocationDetails({ invocation }: { invocation: Invocation }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-col gap-1 text-xs">
      {invocation.commandName && (
        <div className="break-all">
          <Typography.Text code>{invocation.commandName}</Typography.Text>
          {invocation.arguments && <Typography.Text type="secondary"> {invocation.arguments}</Typography.Text>}
        </div>
      )}
      <Typography.Text type="secondary">
        {invocation.model} · {t('assistant.bar.latency', { ms: invocation.latencyMs })} ·{' '}
        {t('assistant.bar.tokens', { prompt: invocation.promptTokens, completion: invocation.completionTokens })}
      </Typography.Text>
    </div>
  )
}

export function AssistantPage() {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') === HISTORY_TAB ? HISTORY_TAB : CHAT_TAB

  const { data: profile, error: profileError } = useAssistantProfile()
  const { data: invocations, isLoading, error: invocationsError } = useInvocations()
  const interpret = useInterpret()
  const interpretAudio = useInterpretAudio()
  const confirmInvocation = useConfirmInvocation()
  const cancelInvocation = useCancelInvocation()
  const voice = useVoiceRecorder(handleVoiceNote)

  const [text, setText] = useState('')
  // undefined follows the most recent conversation; null is a new one with nothing in it yet.
  const [conversationId, setConversationId] = useState<string | null | undefined>(undefined)

  const disabled = profile ? !profile.isEnabled : false
  const thinking = interpret.isPending || interpretAudio.isPending
  const busy = thinking || confirmInvocation.isPending || cancelInvocation.isPending

  const activeConversationId = conversationId === undefined ? invocations?.[0]?.conversationId : conversationId
  const thread = (invocations ?? [])
    .filter((i) => activeConversationId != null && i.conversationId === activeConversationId)
    .slice()
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())

  // Keep the transcript pinned to the newest message.
  const scrollRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const el = scrollRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [thread.length, thinking, tab])

  function showTab(next: string) {
    setSearchParams(next === HISTORY_TAB ? { tab: HISTORY_TAB } : {}, { replace: true })
  }

  function openConversation(id: string) {
    setConversationId(id)
    showTab(CHAT_TAB)
  }

  async function handleSend() {
    const value = text.trim()
    if (!value || disabled || busy) return
    try {
      const result = await interpret.mutateAsync({ text: value, conversationId: activeConversationId ?? undefined })
      setConversationId(result.conversationId)
      setText('')
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  // Enter sends; Shift+Enter breaks the line. Enter that confirms an IME composition is left alone.
  function handleKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key !== 'Enter' || e.shiftKey || e.nativeEvent.isComposing) return
    e.preventDefault()
    handleSend()
  }

  async function handleStartRecording() {
    try {
      await voice.start()
    } catch {
      message.error(t('assistant.bar.micDenied'))
    }
  }

  // The chat shows what was heard: the pipeline records the transcript as the user's message.
  async function handleVoiceNote(recording: Blob) {
    try {
      const result = await interpretAudio.mutateAsync({
        audio: await toWav(recording),
        conversationId: activeConversationId ?? undefined,
      })
      setConversationId(result.conversationId)
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  async function handleConfirm(id: string) {
    try {
      await confirmInvocation.mutateAsync(id)
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  async function handleCancel(id: string) {
    try {
      await cancelInvocation.mutateAsync(id)
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  const userBubble = (content: string) => (
    <div className="flex justify-end">
      <div
        className="max-w-[80%] whitespace-pre-wrap break-words rounded-2xl rounded-br-md px-3.5 py-2"
        style={{ background: token.colorPrimary, color: onAccent(token.colorPrimary) }}
      >
        {content}
      </div>
    </div>
  )

  const assistantRow = (content: ReactNode) => (
    <div className="flex items-start gap-2.5">
      <div
        className="flex size-7 shrink-0 items-center justify-center rounded-full text-sm"
        style={{ background: token.colorFillSecondary, color: token.colorTextSecondary }}
        aria-hidden
      >
        <RobotOutlined />
      </div>
      <div className="flex min-w-0 max-w-[80%] flex-col gap-1.5 pt-0.5">{content}</div>
    </div>
  )

  const transcript =
    thread.length === 0 && !thinking ? (
      <div className="m-auto flex max-w-md flex-col items-center gap-4 py-8 text-center">
        <div
          className="flex size-12 items-center justify-center rounded-full text-xl"
          style={{ background: token.colorPrimaryBg, color: token.colorPrimary }}
          aria-hidden
        >
          <RobotOutlined />
        </div>
        <div className="flex flex-col gap-1">
          <Typography.Text strong className="text-base">
            {t('assistant.bar.emptyTitle')}
          </Typography.Text>
          <Typography.Text type="secondary">{t('assistant.bar.emptyHint')}</Typography.Text>
        </div>
        <div className="flex flex-wrap justify-center gap-2">
          {EXAMPLES.map((key) => (
            <Button key={key} shape="round" disabled={disabled} onClick={() => setText(t(`assistant.bar.examples.${key}`))}>
              {t(`assistant.bar.examples.${key}`)}
            </Button>
          ))}
        </div>
      </div>
    ) : (
      <>
        {thread.map((inv, index) => {
          const reply = inv.result ?? inv.error
          // One sentence can produce several invocations (one per tool call): show the user's
          // message once, above the first of them.
          const prev = thread[index - 1]
          const sameTurn =
            prev !== undefined &&
            prev.utterance === inv.utterance &&
            Math.abs(new Date(inv.createdAt).getTime() - new Date(prev.createdAt).getTime()) < 2000
          return (
            <div key={inv.id} className="flex flex-col gap-3">
              {!sameTurn && userBubble(inv.utterance)}
              {assistantRow(
                <>
                  {!QUIET_STATUSES.includes(inv.status) && (
                    <div>
                      <StatusTag status={inv.status} />
                    </div>
                  )}
                  {reply && (
                    <Typography.Text type={inv.error ? 'danger' : undefined} className="whitespace-pre-wrap break-words">
                      {reply}
                    </Typography.Text>
                  )}
                  {inv.status === 'pending-confirmation' && (
                    <div className="flex gap-2 pt-1">
                      <Button
                        size="small"
                        type="primary"
                        loading={confirmInvocation.isPending}
                        disabled={busy && !confirmInvocation.isPending}
                        onClick={() => handleConfirm(inv.id)}
                      >
                        {t('assistant.bar.confirm')}
                      </Button>
                      <Button
                        size="small"
                        loading={cancelInvocation.isPending}
                        disabled={busy && !cancelInvocation.isPending}
                        onClick={() => handleCancel(inv.id)}
                      >
                        {t('assistant.bar.cancel')}
                      </Button>
                    </div>
                  )}
                  <details className="text-xs">
                    <summary className="w-fit cursor-pointer select-none" style={{ color: token.colorTextTertiary }}>
                      {t('assistant.bar.details')}
                    </summary>
                    <div className="pt-1">
                      <InvocationDetails invocation={inv} />
                    </div>
                  </details>
                </>,
              )}
            </div>
          )
        })}
        {thinking && (
          <div className="flex flex-col gap-3">
            {interpret.isPending && userBubble(interpret.variables?.text ?? '')}
            {assistantRow(
              <div className="flex items-center gap-2">
                <Spin size="small" />
                <Typography.Text type="secondary">{t('assistant.bar.thinking')}</Typography.Text>
              </div>,
            )}
          </div>
        )}
      </>
    )

  const chat = (
    <SettingsSection>
      <div className="flex h-[calc(100dvh-310px)] min-h-[360px] flex-col">
        <div ref={scrollRef} className="flex flex-1 flex-col gap-4 overflow-y-auto p-5">
          {transcript}
        </div>

        <div className="flex items-end gap-2 border-t border-solid p-3" style={{ borderColor: token.colorBorderSecondary }}>
          <Input.TextArea
            autoSize={{ minRows: 1, maxRows: 6 }}
            variant="borderless"
            value={text}
            disabled={disabled || voice.recording}
            placeholder={t(voice.recording ? 'assistant.bar.recording' : 'assistant.bar.placeholder')}
            aria-label={t('assistant.bar.placeholder')}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          {voiceSupported && (
            <Button
              shape="circle"
              danger={voice.recording}
              icon={voice.recording ? <StopOutlined /> : <AudioOutlined />}
              loading={interpretAudio.isPending}
              disabled={disabled || (busy && !voice.recording)}
              title={t(voice.recording ? 'assistant.bar.stopRecording' : 'assistant.bar.record')}
              aria-label={t(voice.recording ? 'assistant.bar.stopRecording' : 'assistant.bar.record')}
              onClick={voice.recording ? voice.stop : handleStartRecording}
            />
          )}
          <Button
            shape="circle"
            type="primary"
            icon={<SendOutlined />}
            loading={interpret.isPending}
            disabled={disabled || voice.recording || busy || !text.trim()}
            title={t('assistant.bar.send')}
            aria-label={t('assistant.bar.send')}
            onClick={handleSend}
          />
        </div>
      </div>
    </SettingsSection>
  )

  const logColumns: ColumnsType<Invocation> = [
    {
      title: t('assistant.bar.colWhen'),
      dataIndex: 'createdAt',
      width: 170,
      render: (value: string) => <Typography.Text type="secondary">{new Date(value).toLocaleString(undefined, { timeZone })}</Typography.Text>,
    },
    {
      title: t('assistant.bar.colRequest'),
      dataIndex: 'utterance',
      ellipsis: { showTitle: true },
    },
    {
      title: t('assistant.bar.colStatus'),
      dataIndex: 'status',
      width: 170,
      render: (status: InvocationStatus) => <StatusTag status={status} />,
    },
    {
      title: t('assistant.bar.colAction'),
      dataIndex: 'commandName',
      width: 190,
      render: (commandName: string | null) =>
        commandName ? (
          <Typography.Text code>{commandName}</Typography.Text>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
  ]

  const history = (
    <SettingsSection>
      <Table<Invocation>
        rowKey="id"
        size="small"
        tableLayout="fixed"
        loading={isLoading}
        columns={logColumns}
        dataSource={invocations ?? []}
        locale={{ emptyText: t('assistant.bar.logEmpty') }}
        pagination={{ pageSize: 15, hideOnSinglePage: true }}
        scroll={{ x: 720 }}
        expandable={{
          expandedRowRender: (item) => (
            <div className="flex flex-col gap-2 py-1">
              {(item.result ?? item.error) && (
                <Typography.Text type={item.error ? 'danger' : undefined} className="whitespace-pre-wrap break-words">
                  {item.result ?? item.error}
                </Typography.Text>
              )}
              <InvocationDetails invocation={item} />
              <Typography.Link className="w-fit" onClick={() => openConversation(item.conversationId)}>
                {t('assistant.bar.openConversation')}
              </Typography.Link>
            </div>
          ),
        }}
      />
    </SettingsSection>
  )

  const loadError = profileError ?? invocationsError
  if (loadError) return <Alert type="error" showIcon title={loadError.message} />

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-4">
      <PageHeading title={t('assistant.bar.title')} description={t('assistant.bar.intro')} />

      {disabled && (
        <Alert
          type="warning"
          showIcon
          title={t('assistant.bar.notEnabled')}
          description={<Link to="/settings/assistant">{t('assistant.bar.goToSettings')}</Link>}
        />
      )}

      <Tabs
        activeKey={tab}
        onChange={showTab}
        tabBarExtraContent={
          tab === CHAT_TAB && (
            <Button
              icon={<PlusOutlined />}
              disabled={thread.length === 0 || busy}
              onClick={() => {
                setConversationId(null)
                setText('')
              }}
            >
              {t('assistant.bar.newConversation')}
            </Button>
          )
        }
        items={[
          { key: CHAT_TAB, label: t('assistant.bar.tabChat'), children: chat },
          { key: HISTORY_TAB, label: t('assistant.bar.logTitle'), children: history },
        ]}
      />
    </div>
  )
}
