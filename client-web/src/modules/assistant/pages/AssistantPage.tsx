import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { RobotOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import type { Invocation, InvocationStatus } from '../models'
import {
  useAssistantProfile,
  useCancelInvocation,
  useConfirmInvocation,
  useInterpret,
  useInvocations,
} from '../hooks/useAssistant'

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

function StatusTag({ status }: { status: InvocationStatus }) {
  const { t } = useTranslation()
  return <Tag color={STATUS_COLOR[status]}>{t(`assistant.bar.status.${status}`)}</Tag>
}

export function AssistantPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()

  const { data: profile } = useAssistantProfile()
  const { data: invocations } = useInvocations()
  const interpret = useInterpret()
  const confirmInvocation = useConfirmInvocation()
  const cancelInvocation = useCancelInvocation()

  const [text, setText] = useState('')
  const [conversationId, setConversationId] = useState<string | undefined>(undefined)

  const disabled = profile ? !profile.isEnabled : false
  const busy = interpret.isPending || confirmInvocation.isPending || cancelInvocation.isPending

  // The console shows the active conversation as a chat; the table below keeps the full log.
  // Default to the most recent conversation until the user starts a new exchange.
  const activeConversationId = conversationId ?? invocations?.[0]?.conversationId
  const thread = (invocations ?? [])
    .filter((i) => !activeConversationId || i.conversationId === activeConversationId)
    .slice()
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())

  // Keep the transcript pinned to the newest message.
  const scrollRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const el = scrollRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [thread.length, busy])

  async function handleSend() {
    const value = text.trim()
    if (!value) return
    try {
      const result = await interpret.mutateAsync({ text: value, conversationId })
      setConversationId(result.conversationId)
      setText('')
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

  const logColumns: ColumnsType<Invocation> = [
    {
      title: t('assistant.bar.colWhen'),
      dataIndex: 'createdAt',
      width: 160,
      render: (value: string) => new Date(value).toLocaleString(),
    },
    {
      title: t('assistant.bar.colRequest'),
      dataIndex: 'utterance',
      width: 260,
      ellipsis: true,
      render: (utterance: string) => (
        <Typography.Text ellipsis={{ tooltip: utterance }}>{utterance}</Typography.Text>
      ),
    },
    {
      title: t('assistant.bar.colStatus'),
      dataIndex: 'status',
      width: 130,
      render: (status: InvocationStatus) => <StatusTag status={status} />,
    },
    {
      title: t('assistant.bar.colAction'),
      dataIndex: 'commandName',
      width: 190,
      render: (commandName: string | null, item) =>
        commandName ? (
          <div className="truncate" title={`${commandName}${item.arguments ? ` ${item.arguments}` : ''}`}>
            <Typography.Text code>{commandName}</Typography.Text>
            {item.arguments ? ` ${item.arguments}` : ''}
          </div>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: t('assistant.bar.colResult'),
      key: 'result',
      width: 220,
      render: (_, item) => {
        const value = item.result ?? item.error
        return value ? (
          <div className="truncate" title={value}>
            <Typography.Text type={item.error ? 'danger' : 'secondary'}>{value}</Typography.Text>
          </div>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        )
      },
    },
    {
      title: t('assistant.bar.colModel'),
      dataIndex: 'model',
      width: 180,
      render: (model: string, item) => (
        <Typography.Text type="secondary" className="text-xs">
          {model} · {t('assistant.bar.latency', { ms: item.latencyMs })} ·{' '}
          {t('assistant.bar.tokens', { prompt: item.promptTokens, completion: item.completionTokens })}
        </Typography.Text>
      ),
    },
  ]

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6">
      <PageHeading title={t('assistant.bar.title')} description={t('assistant.bar.intro')} />

      {disabled && (
        <Alert
          type="warning"
          showIcon
          message={t('assistant.bar.notEnabled')}
          description={<Link to="/settings/assistant">{t('assistant.bar.goToSettings')}</Link>}
        />
      )}

      {/* Console: a chat transcript with the input pinned to the bottom. */}
      <SettingsSection title={t('assistant.bar.consoleTitle')}>
        <div className="flex flex-col">
          <div
            ref={scrollRef}
            className="flex max-h-[460px] min-h-[220px] flex-col gap-3 overflow-y-auto p-4"
            style={{ background: token.colorFillQuaternary }}
          >
            {thread.length === 0 ? (
              <div className="m-auto text-center">
                <Typography.Text type="secondary">{t('assistant.bar.resultEmpty')}</Typography.Text>
              </div>
            ) : (
              thread.map((inv) => {
                const reply = inv.result ?? inv.error
                return (
                  <div key={inv.id} className="flex flex-col gap-2">
                    {/* User message, right-aligned */}
                    <div className="flex justify-end">
                      <div
                        className="max-w-[80%] whitespace-pre-wrap break-words rounded-2xl px-3.5 py-2 text-sm"
                        style={{ background: token.colorPrimary, color: token.colorTextLightSolid }}
                      >
                        {inv.utterance}
                      </div>
                    </div>
                    {/* Assistant reply, left-aligned */}
                    <div className="flex justify-start">
                      <div
                        className="flex max-w-[80%] flex-col gap-1.5 rounded-2xl px-3.5 py-2"
                        style={{
                          background: token.colorBgElevated,
                          border: `1px solid ${token.colorBorderSecondary}`,
                        }}
                      >
                        <div className="flex flex-wrap items-center gap-2">
                          <StatusTag status={inv.status} />
                          {inv.commandName && (
                            <Typography.Text code className="text-xs">
                              {inv.commandName}
                              {inv.arguments ? ` ${inv.arguments}` : ''}
                            </Typography.Text>
                          )}
                        </div>
                        {reply && (
                          <Typography.Text
                            type={inv.error ? 'danger' : undefined}
                            className="whitespace-pre-wrap break-words text-sm"
                          >
                            {reply}
                          </Typography.Text>
                        )}
                        {inv.status === 'pending-confirmation' && (
                          <div className="flex gap-2 pt-1">
                            <Button
                              size="small"
                              type="primary"
                              loading={confirmInvocation.isPending}
                              onClick={() => handleConfirm(inv.id)}
                            >
                              {t('assistant.bar.confirm')}
                            </Button>
                            <Button
                              size="small"
                              loading={cancelInvocation.isPending}
                              onClick={() => handleCancel(inv.id)}
                            >
                              {t('assistant.bar.cancel')}
                            </Button>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                )
              })
            )}
          </div>

          {/* Input row, pinned to the bottom of the console. */}
          <div
            className="flex gap-2 border-t border-solid p-3"
            style={{ borderColor: token.colorBorderSecondary }}
          >
            <Input
              size="large"
              prefix={<RobotOutlined className="opacity-60" />}
              value={text}
              disabled={disabled || busy}
              placeholder={t('assistant.bar.placeholder')}
              onChange={(e) => setText(e.target.value)}
              onPressEnter={handleSend}
            />
            <Button
              size="large"
              type="primary"
              loading={interpret.isPending}
              disabled={disabled}
              onClick={handleSend}
            >
              {t('assistant.bar.send')}
            </Button>
          </div>
        </div>
      </SettingsSection>

      {/* Full invocation history */}
      <SettingsSection title={t('assistant.bar.logTitle')}>
        <Table<Invocation>
          rowKey="id"
          size="small"
          tableLayout="fixed"
          columns={logColumns}
          dataSource={invocations ?? []}
          locale={{ emptyText: t('assistant.bar.logEmpty') }}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          scroll={{ x: 1140 }}
        />
      </SettingsSection>
    </div>
  )
}
