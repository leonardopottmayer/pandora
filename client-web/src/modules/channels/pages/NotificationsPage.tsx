import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Alert,
  App,
  Button,
  Checkbox,
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
  theme,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { SendOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
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
  const { token } = theme.useToken()
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
      width: 160,
      render: (value: string) => new Date(value).toLocaleString(),
    },
    {
      title: t('notifications.colCategory'),
      dataIndex: 'category',
      width: 130,
      ellipsis: true,
      render: (_: string | null, row) => row.category ?? row.templateKey,
    },
    {
      title: t('notifications.colChannel'),
      dataIndex: 'channel',
      width: 100,
      render: (channel: ChannelId) => <Tag>{channelLabel(channel)}</Tag>,
    },
    {
      title: t('notifications.colStatus'),
      dataIndex: 'status',
      width: 110,
      render: (status: NotificationStatus) => (
        <Tag color={STATUS_COLOR[status]}>{t(`notifications.status.${status}`)}</Tag>
      ),
    },
    {
      title: t('notifications.colAttempts'),
      dataIndex: 'attemptCount',
      width: 90,
      align: 'center',
    },
    {
      title: t('notifications.colError'),
      dataIndex: 'lastError',
      width: 200,
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

  const cardStyle = {
    border: `1px solid ${token.colorBorderSecondary}`,
    background: token.colorBgContainer,
  }

  async function toggleChannel(category: string, channel: ChannelId, checked: boolean) {
    const current = channelsFor(category)
    const next = checked ? [...current, channel] : current.filter((c) => c !== channel)
    await handlePreferenceChange(category, next)
  }

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6">
      <PageHeading title={t('notifications.pageTitle')} description={t('notifications.pageSubtitle')} />

      <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-[minmax(320px,380px)_1fr]">
        {/* LEFT: channels + quiet hours */}
        <div className="flex flex-col gap-6">
          <section className="flex flex-col gap-2.5">
            <div className="px-0.5">
              <Typography.Text strong type="secondary" className="text-xs uppercase tracking-wider">
                {t('notifications.channelsTitle')}
              </Typography.Text>
            </div>

            {channelsLoading ? (
              <div className="rounded-xl p-6" style={cardStyle}>
                <Spin />
              </div>
            ) : (
              <>
                {/* Telegram */}
                <div className="flex flex-col gap-2 rounded-xl p-4" style={cardStyle}>
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
                      <Typography.Text type="secondary">{t('notifications.telegramDesc')}</Typography.Text>
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

                {/* E-mail */}
                <div className="flex flex-col gap-2 rounded-xl p-4" style={cardStyle}>
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
              </>
            )}
          </section>

          {settingsLoading || !settings ? (
            <SettingsSection title={t('notifications.quietHoursTitle')} bodyClassName="p-6">
              <Spin />
            </SettingsSection>
          ) : (
            <QuietHoursCard settings={settings} />
          )}
        </div>

        {/* RIGHT: what goes where + delivery history */}
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-2">
            <SettingsSection title={t('notifications.preferencesTitle')}>
              {prefsLoading ? (
                <div className="p-6">
                  <Spin />
                </div>
              ) : (
                <Table<{ key: string; category: string }>
                  rowKey="key"
                  size="small"
                  pagination={false}
                  dataSource={NOTIFICATION_CATEGORIES.map((c) => ({ key: c, category: c }))}
                  columns={[
                    {
                      title: t('notifications.colCategory'),
                      dataIndex: 'category',
                      render: (category: string) => (
                        <Typography.Text strong>{t(`notifications.category.${category}`)}</Typography.Text>
                      ),
                    },
                    ...ALL_CHANNELS.map((ch) => ({
                      title: channelLabel(ch),
                      key: ch,
                      align: 'center' as const,
                      width: 120,
                      render: (_: unknown, row: { category: string }) => (
                        <Checkbox
                          checked={channelsFor(row.category).includes(ch)}
                          onChange={(e) => toggleChannel(row.category, ch, e.target.checked)}
                        />
                      ),
                    })),
                  ]}
                />
              )}
            </SettingsSection>
            <Typography.Text type="secondary" className="px-0.5 text-xs">
              {t('notifications.mutedHint')}
            </Typography.Text>
          </div>

          <SettingsSection
            title={t('notifications.historyTitle')}
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
            {historyError ? (
              <div className="p-4">
                <Alert type="error" showIcon message={t('notifications.historyError')} />
              </div>
            ) : (
              <Table<NotificationHistoryItem>
                rowKey="id"
                size="small"
                tableLayout="fixed"
                loading={historyLoading}
                columns={historyColumns}
                dataSource={history ?? []}
                locale={{ emptyText: t('notifications.historyEmpty') }}
                pagination={{ pageSize: 10, hideOnSinglePage: true }}
                scroll={{ x: 790 }}
              />
            )}
          </SettingsSection>
        </div>
      </div>
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
    <div className="flex flex-col gap-3">
      <SettingsSection title={t('notifications.quietHoursTitle')}>
        <SettingRow
          label={t('notifications.quietHoursEnable')}
          description={t('notifications.quietHoursDesc')}
          align="start"
          control={<Switch checked={enabled} onChange={setEnabled} />}
        />

        {enabled && (
          <>
            <SettingRow
              vertical
              label={t('notifications.quietHoursInterval')}
              control={
                <Space wrap>
                  <TimePicker
                    format={TIME_FORMAT}
                    minuteStep={15}
                    value={start}
                    onChange={setStart}
                    allowClear={false}
                    placeholder={t('notifications.quietHoursFrom')}
                  />
                  <span className="opacity-60">→</span>
                  <TimePicker
                    format={TIME_FORMAT}
                    minuteStep={15}
                    value={end}
                    onChange={setEnd}
                    allowClear={false}
                    placeholder={t('notifications.quietHoursTo')}
                  />
                </Space>
              }
            />
            <SettingRow
              label={t('notifications.quietHoursBehaviour')}
              control={
                <Select<QuietHoursBehaviour>
                  style={{ width: 200 }}
                  value={behaviour}
                  onChange={setBehaviour}
                  options={QUIET_HOURS_BEHAVIOURS.map((b) => ({
                    label: t(`notifications.quietHoursBehaviourOption.${b}`),
                    value: b,
                  }))}
                />
              }
            />
          </>
        )}
      </SettingsSection>

      {enabled && (
        <Typography.Text type="secondary" className="px-0.5 text-xs">
          {t('notifications.quietHoursZoneHint')}
        </Typography.Text>
      )}

      <div>
        <Button type="primary" onClick={handleSave} loading={setSettings.isPending}>
          {t('notifications.quietHoursSave')}
        </Button>
      </div>
    </div>
  )
}
