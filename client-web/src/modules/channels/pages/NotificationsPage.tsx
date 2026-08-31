import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Divider,
  Popconfirm,
  Select,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  TimePicker,
  Tooltip,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { SendOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  ALL_CHANNELS,
  NOTIFICATION_CATEGORIES,
  NOTIFICATION_STATUSES,
  QUIET_HOURS_BEHAVIOURS,
  type ChannelId,
  type NotificationHistoryItem,
  type NotificationSettings,
  type NotificationStatus,
  type QuietHoursBehaviour,
} from '../models'
import {
  useChannels,
  useDeliveryHistory,
  useLinkChannel,
  useNotificationPreferences,
  useNotificationSettings,
  useSetNotificationSettings,
  useSetPreference,
  useTestChannel,
  useUnlinkChannel,
} from '../hooks/useChannels'

const TIME_FORMAT = 'HH:mm'

function channelLabel(channel: ChannelId): string {
  return channel === 'email' ? 'E-mail' : 'Telegram'
}

const STATUS_COLOR: Record<NotificationStatus, string> = {
  Pending: 'default',
  Sending: 'blue',
  Sent: 'green',
  Failed: 'orange',
  Dead: 'red',
}

export function NotificationsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data: channels, isLoading: channelsLoading } = useChannels()
  const { data: preferences, isLoading: prefsLoading } = useNotificationPreferences()

  const { data: settings, isLoading: settingsLoading } = useNotificationSettings()

  const link = useLinkChannel()
  const unlink = useUnlinkChannel()
  const test = useTestChannel()
  const setPreference = useSetPreference()

  const [telegramLinkUrl, setTelegramLinkUrl] = useState<string | null>(null)
  const [historyStatus, setHistoryStatus] = useState<NotificationStatus | undefined>(undefined)
  const { data: history, isLoading: historyLoading, isError: historyError } = useDeliveryHistory({
    status: historyStatus,
  })

  const telegram = channels?.find((c) => c.channel === 'telegram')
  const email = channels?.find((c) => c.channel === 'email')

  async function handleConnectTelegram() {
    try {
      const result = await link.mutateAsync('telegram')
      setTelegramLinkUrl(result.url)
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.connectError')))
    }
  }

  async function handleUnlink(channel: ChannelId) {
    try {
      await unlink.mutateAsync(channel)
      message.success(t('notifications.disconnected'))
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.saveError')))
    }
  }

  async function handleTest(channel: ChannelId) {
    try {
      await test.mutateAsync(channel)
      message.success(t('notifications.testSent'))
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.testError')))
    }
  }

  function channelsFor(category: string): ChannelId[] {
    const row = preferences?.find((p) => p.category === category)
    // No row means "no choice made": the backend defaults to every usable channel.
    return row ? row.channels : ALL_CHANNELS
  }

  async function handlePreferenceChange(category: string, selected: ChannelId[]) {
    try {
      await setPreference.mutateAsync({ category, channels: selected })
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.saveError')))
    }
  }

  const historyColumns: ColumnsType<NotificationHistoryItem> = [
    {
      title: t('notifications.colWhen'),
      dataIndex: 'createdAt',
      render: (value: string) => new Date(value).toLocaleString(),
    },
    {
      title: t('notifications.colCategory'),
      dataIndex: 'category',
      render: (_: string | null, row) => row.category ?? row.templateKey,
    },
    {
      title: t('notifications.colChannel'),
      dataIndex: 'channel',
      render: (channel: ChannelId) => <Tag>{channelLabel(channel)}</Tag>,
    },
    {
      title: t('notifications.colStatus'),
      dataIndex: 'status',
      render: (status: NotificationStatus) => (
        <Tag color={STATUS_COLOR[status]}>{t(`notifications.status.${status}`)}</Tag>
      ),
    },
    {
      title: t('notifications.colAttempts'),
      dataIndex: 'attemptCount',
      align: 'center',
    },
    {
      title: t('notifications.colError'),
      dataIndex: 'lastError',
      ellipsis: true,
      render: (error: string | null) =>
        error ? (
          <Tooltip title={error}>
            <Typography.Text type="danger">{error}</Typography.Text>
          </Tooltip>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
  ]

  return (
    <div className="mx-auto max-w-2xl">
      <Card title={t('notifications.channelsTitle')}>
        {channelsLoading ? (
          <Spin />
        ) : (
          <Space direction="vertical" size="large" className="w-full">
            {/* Telegram */}
            <div className="flex flex-col gap-2">
              <Space>
                <Typography.Text strong>Telegram</Typography.Text>
                {telegram?.isVerified && telegram.isEnabled && (
                  <Tag color="green">{t('notifications.connected')}</Tag>
                )}
                {telegram && !telegram.isEnabled && (
                  <Tag color="red">{t('notifications.disabled')}</Tag>
                )}
              </Space>

              {telegram?.isVerified ? (
                <>
                  <Typography.Text type="secondary">{telegram.address}</Typography.Text>
                  {!telegram.isEnabled && telegram.disabledReason && (
                    <Alert
                      type="warning"
                      showIcon
                      message={t('notifications.disabledReason', { reason: telegram.disabledReason })}
                    />
                  )}
                  <Space>
                    <Button
                      icon={<SendOutlined />}
                      onClick={() => handleTest('telegram')}
                      loading={test.isPending}
                    >
                      {t('notifications.sendTest')}
                    </Button>
                    <Popconfirm
                      title={t('notifications.disconnectConfirm')}
                      onConfirm={() => handleUnlink('telegram')}
                    >
                      <Button danger>{t('notifications.disconnect')}</Button>
                    </Popconfirm>
                  </Space>
                </>
              ) : (
                <>
                  <Typography.Text type="secondary">
                    {t('notifications.telegramDesc')}
                  </Typography.Text>
                  <Button
                    type="primary"
                    className="w-fit"
                    onClick={handleConnectTelegram}
                    loading={link.isPending}
                  >
                    {t('notifications.connectTelegram')}
                  </Button>
                  {telegramLinkUrl && (
                    <Alert
                      type="info"
                      showIcon
                      message={t('notifications.linkReady')}
                      description={
                        <Space direction="vertical">
                          <span>{t('notifications.linkInstructions')}</span>
                          <Button type="primary" href={telegramLinkUrl} target="_blank">
                            {t('notifications.openTelegram')}
                          </Button>
                        </Space>
                      }
                    />
                  )}
                </>
              )}
            </div>

            <Divider className="my-0" />

            {/* E-mail */}
            <div className="flex flex-col gap-2">
              <Typography.Text strong>E-mail</Typography.Text>
              {email ? (
                <>
                  <Typography.Text type="secondary">{email.address}</Typography.Text>
                  <Button
                    className="w-fit"
                    icon={<SendOutlined />}
                    onClick={() => handleTest('email')}
                    loading={test.isPending}
                  >
                    {t('notifications.sendTest')}
                  </Button>
                </>
              ) : (
                <Typography.Text type="secondary">{t('notifications.emailFromAccount')}</Typography.Text>
              )}
            </div>
          </Space>
        )}
      </Card>

      <Card title={t('notifications.preferencesTitle')} className="mt-4">
        <Typography.Paragraph type="secondary">
          {t('notifications.preferencesDesc')}
        </Typography.Paragraph>
        {prefsLoading ? (
          <Spin />
        ) : (
          <Space direction="vertical" size="middle" className="w-full">
            {NOTIFICATION_CATEGORIES.map((category) => (
              <div key={category} className="flex flex-col gap-1">
                <Typography.Text strong>{t(`notifications.category.${category}`)}</Typography.Text>
                <Checkbox.Group
                  value={channelsFor(category)}
                  onChange={(v) => handlePreferenceChange(category, v as ChannelId[])}
                  options={ALL_CHANNELS.map((c) => ({ label: channelLabel(c), value: c }))}
                />
              </div>
            ))}
            <Typography.Text type="secondary">{t('notifications.mutedHint')}</Typography.Text>
          </Space>
        )}
      </Card>

      {settingsLoading || !settings ? (
        <Card title={t('notifications.quietHoursTitle')} className="mt-4">
          <Spin />
        </Card>
      ) : (
        <QuietHoursCard settings={settings} />
      )}

      <Card
        title={t('notifications.historyTitle')}
        className="mt-4"
        extra={
          <Select<NotificationStatus>
            allowClear
            style={{ width: 160 }}
            placeholder={t('notifications.filterStatus')}
            value={historyStatus}
            onChange={(value) => setHistoryStatus(value)}
            options={NOTIFICATION_STATUSES.map((s) => ({
              label: t(`notifications.status.${s}`),
              value: s,
            }))}
          />
        }
      >
        <Typography.Paragraph type="secondary">
          {t('notifications.historyDesc')}
        </Typography.Paragraph>
        {historyError ? (
          <Alert type="error" showIcon message={t('notifications.historyError')} />
        ) : (
          <Table<NotificationHistoryItem>
            rowKey="id"
            size="small"
            loading={historyLoading}
            columns={historyColumns}
            dataSource={history ?? []}
            locale={{ emptyText: t('notifications.historyEmpty') }}
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            scroll={{ x: true }}
          />
        )}
      </Card>
    </div>
  )
}

/**
 * Quiet-hours editor. Rendered only once the settings have loaded, so it seeds its form state
 * straight from props in the useState initializers — no effect, no sync.
 */
function QuietHoursCard({ settings }: { settings: NotificationSettings }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const setSettings = useSetNotificationSettings()

  const [enabled, setEnabled] = useState(settings.quietHoursEnabled)
  const [start, setStart] = useState<Dayjs | null>(
    settings.quietHoursStart ? dayjs(settings.quietHoursStart, TIME_FORMAT) : null,
  )
  const [end, setEnd] = useState<Dayjs | null>(
    settings.quietHoursEnd ? dayjs(settings.quietHoursEnd, TIME_FORMAT) : null,
  )
  const [behaviour, setBehaviour] = useState<QuietHoursBehaviour>(
    settings.quietHoursBehaviour ?? 'suppress',
  )

  async function handleSave() {
    if (enabled && (!start || !end || start.format(TIME_FORMAT) === end.format(TIME_FORMAT))) {
      message.error(t('notifications.quietHoursInvalid'))
      return
    }
    try {
      await setSettings.mutateAsync({
        quietHoursEnabled: enabled,
        quietHoursStart: enabled ? start!.format(TIME_FORMAT) : null,
        quietHoursEnd: enabled ? end!.format(TIME_FORMAT) : null,
        quietHoursBehaviour: enabled ? behaviour : null,
      })
      message.success(t('notifications.quietHoursSaved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.saveError')))
    }
  }

  return (
    <Card title={t('notifications.quietHoursTitle')} className="mt-4">
      <Typography.Paragraph type="secondary">
        {t('notifications.quietHoursDesc')}
      </Typography.Paragraph>
      <Space direction="vertical" size="middle" className="w-full">
        <Space>
          <Switch checked={enabled} onChange={setEnabled} />
          <Typography.Text>{t('notifications.quietHoursEnable')}</Typography.Text>
        </Space>

        {enabled && (
          <>
            <Space wrap>
              <div className="flex flex-col gap-1">
                <Typography.Text type="secondary">{t('notifications.quietHoursFrom')}</Typography.Text>
                <TimePicker
                  format={TIME_FORMAT}
                  minuteStep={15}
                  value={start}
                  onChange={setStart}
                  allowClear={false}
                />
              </div>
              <div className="flex flex-col gap-1">
                <Typography.Text type="secondary">{t('notifications.quietHoursTo')}</Typography.Text>
                <TimePicker
                  format={TIME_FORMAT}
                  minuteStep={15}
                  value={end}
                  onChange={setEnd}
                  allowClear={false}
                />
              </div>
              <div className="flex flex-col gap-1">
                <Typography.Text type="secondary">{t('notifications.quietHoursBehaviour')}</Typography.Text>
                <Select<QuietHoursBehaviour>
                  style={{ width: 200 }}
                  value={behaviour}
                  onChange={setBehaviour}
                  options={QUIET_HOURS_BEHAVIOURS.map((b) => ({
                    label: t(`notifications.quietHoursBehaviourOption.${b}`),
                    value: b,
                  }))}
                />
              </div>
            </Space>
            <Typography.Text type="secondary">{t('notifications.quietHoursZoneHint')}</Typography.Text>
          </>
        )}

        <Button type="primary" className="w-fit" onClick={handleSave} loading={setSettings.isPending}>
          {t('notifications.quietHoursSave')}
        </Button>
      </Space>
    </Card>
  )
}
