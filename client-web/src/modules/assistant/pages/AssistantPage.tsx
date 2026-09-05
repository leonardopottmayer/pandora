import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Card, Empty, Input, List, Space, Spin, Tag, Typography } from 'antd'
import { RobotOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { InterpretResult, Invocation, InvocationStatus } from '../models'
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

  const { data: profile } = useAssistantProfile()
  const { data: invocations, isLoading: invocationsLoading } = useInvocations()
  const interpret = useInterpret()
  const confirmInvocation = useConfirmInvocation()
  const cancelInvocation = useCancelInvocation()

  const [text, setText] = useState('')
  const [conversationId, setConversationId] = useState<string | undefined>(undefined)
  const [last, setLast] = useState<InterpretResult | null>(null)

  const disabled = profile ? !profile.isEnabled : false
  const busy = interpret.isPending || confirmInvocation.isPending || cancelInvocation.isPending

  async function handleSend() {
    const value = text.trim()
    if (!value) return
    try {
      const result = await interpret.mutateAsync({ text: value, conversationId })
      setLast(result)
      setConversationId(result.conversationId)
      setText('')
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  async function handleConfirm(id: string) {
    try {
      setLast(await confirmInvocation.mutateAsync(id))
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  async function handleCancel(id: string) {
    try {
      setLast(await cancelInvocation.mutateAsync(id))
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.bar.error')))
    }
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4">
      <Card title={<Space><RobotOutlined />{t('assistant.bar.title')}</Space>}>
        <Typography.Paragraph type="secondary">{t('assistant.bar.intro')}</Typography.Paragraph>

        {disabled && (
          <Alert
            type="warning"
            showIcon
            className="mb-4"
            message={t('assistant.bar.notEnabled')}
            description={<Link to="/settings/assistant">{t('assistant.bar.goToSettings')}</Link>}
          />
        )}

        <Space.Compact className="w-full">
          <Input
            size="large"
            value={text}
            disabled={disabled || busy}
            placeholder={t('assistant.bar.placeholder')}
            onChange={(e) => setText(e.target.value)}
            onPressEnter={handleSend}
          />
          <Button size="large" type="primary" loading={interpret.isPending} disabled={disabled} onClick={handleSend}>
            {t('assistant.bar.send')}
          </Button>
        </Space.Compact>

        {last && (
          <div className="mt-4">
            <Space direction="vertical" size="small" className="w-full">
              <Space>
                <StatusTag status={last.status} />
                {last.commandName && <Typography.Text code>{last.commandName}</Typography.Text>}
              </Space>
              <Typography.Text>{last.message}</Typography.Text>
              {last.status === 'pending-confirmation' && (
                <Space>
                  <Button
                    type="primary"
                    loading={confirmInvocation.isPending}
                    onClick={() => handleConfirm(last.invocationId)}
                  >
                    {t('assistant.bar.confirm')}
                  </Button>
                  <Button loading={cancelInvocation.isPending} onClick={() => handleCancel(last.invocationId)}>
                    {t('assistant.bar.cancel')}
                  </Button>
                </Space>
              )}
            </Space>
          </div>
        )}
      </Card>

      <Card title={t('assistant.bar.logTitle')} loading={invocationsLoading}>
        {invocations && invocations.length > 0 ? (
          <List
            dataSource={invocations}
            renderItem={(item) => <InvocationRow item={item} />}
          />
        ) : (
          !invocationsLoading && <Empty description={t('assistant.bar.logEmpty')} />
        )}
      </Card>

      {busy && <Spin className="self-center" />}
    </div>
  )
}

function InvocationRow({ item }: { item: Invocation }) {
  const { t } = useTranslation()
  return (
    <List.Item>
      <div className="flex w-full flex-col gap-1">
        <div className="flex items-center justify-between gap-2">
          <Typography.Text strong>{item.utterance}</Typography.Text>
          <StatusTag status={item.status} />
        </div>
        {item.commandName && (
          <Typography.Text type="secondary" className="text-xs">
            <Typography.Text code>{item.commandName}</Typography.Text>
            {item.arguments ? ` ${item.arguments}` : ''}
          </Typography.Text>
        )}
        {(item.result || item.error) && (
          <Typography.Text type={item.error ? 'danger' : 'secondary'} className="text-xs">
            {item.result ?? item.error}
          </Typography.Text>
        )}
        <Typography.Text type="secondary" className="text-xs">
          {new Date(item.createdAt).toLocaleString()} · {item.model} ·{' '}
          {t('assistant.bar.latency', { ms: item.latencyMs })} ·{' '}
          {t('assistant.bar.tokens', { prompt: item.promptTokens, completion: item.completionTokens })}
        </Typography.Text>
      </div>
    </List.Item>
  )
}
