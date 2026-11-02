import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Checkbox, Popconfirm, Select, Space, Spin, Switch, Table, Tag, TimePicker, Typography } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { MailOutlined, SendOutlined } from '@ant-design/icons'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  ALL_CHANNELS,
  channelLabel,
  NOTIFICATION_CATEGORIES,
  QUIET_HOURS_BEHAVIOURS,
  type ChannelId,
  type NotificationSettings,
  type QuietHoursBehaviour,
} from '../models'
import {
  useChannels,
  useLinkChannel,
  useNotificationPreferences,
  useNotificationSettings,
  useSetNotificationSettings,
  useSetPreference,
  useTestChannel,
  useUnlinkChannel,
} from '../hooks/useChannels'

const TIME_FORMAT = 'HH:mm'
const DEFAULT_START = '22:00'
const DEFAULT_END = '07:00'

function ChannelsSection() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data: channels, isLoading } = useChannels()
  const link = useLinkChannel()
  const unlink = useUnlinkChannel()
  const test = useTestChannel()
  const [telegramLinkUrl, setTelegramLinkUrl] = useState<string | null>(null)

  const telegram = channels?.find((c) => c.channel === 'telegram')
  const email = channels?.find((c) => c.channel === 'email')

  async function connectTelegram() {
    try {
      setTelegramLinkUrl((await link.mutateAsync('telegram')).url)
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.connectError')))
    }
  }

  async function disconnect(channel: ChannelId) {
    try {
      await unlink.mutateAsync(channel)
      message.success(t('notifications.disconnected'))
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.saveError')))
    }
  }

  async function sendTest(channel: ChannelId) {
    try {
      await test.mutateAsync(channel)
      message.success(t('notifications.testSent'))
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.testError')))
    }
  }

  const testButton = (channel: ChannelId) => (
    <Button icon={<SendOutlined />} onClick={() => sendTest(channel)} loading={test.isPending && test.variables === channel}>
      {t('notifications.sendTest')}
    </Button>
  )

  if (isLoading) {
    return (
      <SettingsSection title={t('notifications.channelsTitle')} bodyClassName="p-6">
        <Spin />
      </SettingsSection>
    )
  }

  return (
    <SettingsSection title={t('notifications.channelsTitle')}>
      <SettingRow
        label={
          <Space size={8}>
            <SendOutlined aria-hidden />
            Telegram
            {telegram?.isVerified && telegram.isEnabled && <Tag color="green">{t('notifications.connected')}</Tag>}
            {telegram && !telegram.isEnabled && <Tag color="red">{t('notifications.disabled')}</Tag>}
          </Space>
        }
        description={telegram?.isVerified ? telegram.address : t('notifications.telegramDesc')}
        control={
          telegram?.isVerified ? (
            <Space>
              {testButton('telegram')}
              <Popconfirm title={t('notifications.disconnectConfirm')} onConfirm={() => disconnect('telegram')}>
                <Button danger>{t('notifications.disconnect')}</Button>
              </Popconfirm>
            </Space>
          ) : (
            <Button type="primary" onClick={connectTelegram} loading={link.isPending}>
              {t('notifications.connectTelegram')}
            </Button>
          )
        }
      />
      {telegram?.isVerified && !telegram.isEnabled && telegram.disabledReason && (
        <div className="px-4 pb-4">
          <Alert type="warning" showIcon title={t('notifications.disabledReason', { reason: telegram.disabledReason })} />
        </div>
      )}
      {!telegram?.isVerified && telegramLinkUrl && (
        <div className="px-4 pb-4">
          <Alert
            type="info"
            showIcon
            title={t('notifications.linkReady')}
            description={t('notifications.linkInstructions')}
            action={
              <Button type="primary" href={telegramLinkUrl} target="_blank">
                {t('notifications.openTelegram')}
              </Button>
            }
          />
        </div>
      )}
      <SettingRow
        label={
          <Space size={8}>
            <MailOutlined aria-hidden />
            E-mail
            {email && <Tag color="green">{t('notifications.connected')}</Tag>}
          </Space>
        }
        description={email?.address ?? t('notifications.emailFromAccount')}
        control={email ? testButton('email') : null}
      />
    </SettingsSection>
  )
}

function PreferencesSection() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data: preferences, isLoading } = useNotificationPreferences()
  const setPreference = useSetPreference()

  // No row means "no choice made": the backend defaults to every usable channel.
  const channelsFor = (category: string): ChannelId[] =>
    preferences?.find((p) => p.category === category)?.channels ?? ALL_CHANNELS

  async function toggle(category: string, channel: ChannelId, checked: boolean) {
    const current = channelsFor(category)
    try {
      await setPreference.mutateAsync({
        category,
        channels: checked ? [...current, channel] : current.filter((c) => c !== channel),
      })
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.saveError')))
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <SettingsSection title={t('notifications.preferencesTitle')}>
        {isLoading ? (
          <div className="p-6">
            <Spin />
          </div>
        ) : (
          <Table<{ category: string }>
            rowKey="category"
            size="small"
            pagination={false}
            dataSource={NOTIFICATION_CATEGORIES.map((category) => ({ category }))}
            columns={[
              {
                title: t('notifications.colCategory'),
                dataIndex: 'category',
                render: (category: string) => t(`notifications.category.${category}`),
              },
              ...ALL_CHANNELS.map((channel) => ({
                title: channelLabel(channel),
                key: channel,
                align: 'center' as const,
                width: 110,
                render: (_: unknown, row: { category: string }) => (
                  <Checkbox
                    aria-label={`${t(`notifications.category.${row.category}`)}: ${channelLabel(channel)}`}
                    checked={channelsFor(row.category).includes(channel)}
                    onChange={(e) => toggle(row.category, channel, e.target.checked)}
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
  )
}

/** Seeds its state from props, so it renders only once the settings have loaded. Saves on every valid change. */
function QuietHours({ settings }: { settings: NotificationSettings }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const save = useSetNotificationSettings()

  const [enabled, setEnabled] = useState(settings.quietHoursEnabled)
  const [start, setStart] = useState<Dayjs>(dayjs(settings.quietHoursStart ?? DEFAULT_START, TIME_FORMAT))
  const [end, setEnd] = useState<Dayjs>(dayjs(settings.quietHoursEnd ?? DEFAULT_END, TIME_FORMAT))
  const [behaviour, setBehaviour] = useState<QuietHoursBehaviour>(settings.quietHoursBehaviour ?? 'suppress')

  const invalid = enabled && start.format(TIME_FORMAT) === end.format(TIME_FORMAT)

  async function persist(next: { enabled: boolean; start: Dayjs; end: Dayjs; behaviour: QuietHoursBehaviour }) {
    if (next.enabled && next.start.format(TIME_FORMAT) === next.end.format(TIME_FORMAT)) return
    try {
      await save.mutateAsync({
        quietHoursEnabled: next.enabled,
        quietHoursStart: next.enabled ? next.start.format(TIME_FORMAT) : null,
        quietHoursEnd: next.enabled ? next.end.format(TIME_FORMAT) : null,
        quietHoursBehaviour: next.enabled ? next.behaviour : null,
      })
    } catch (err) {
      message.error(toErrorMessage(err, t('notifications.saveError')))
    }
  }

  const current = { enabled, start, end, behaviour }

  return (
    <div className="flex flex-col gap-2">
      <SettingsSection title={t('notifications.quietHoursTitle')}>
        <SettingRow
          label={t('notifications.quietHoursEnable')}
          description={t('notifications.quietHoursDesc')}
          control={
            <Switch
              aria-label={t('notifications.quietHoursEnable')}
              checked={enabled}
              onChange={(next) => {
                setEnabled(next)
                void persist({ ...current, enabled: next })
              }}
            />
          }
        />
        {enabled && (
          <>
            <SettingRow
              label={t('notifications.quietHoursInterval')}
              description={t('notifications.quietHoursZoneHint')}
              control={
                <Space wrap>
                  <TimePicker
                    format={TIME_FORMAT}
                    minuteStep={15}
                    value={start}
                    allowClear={false}
                    aria-label={t('notifications.quietHoursFrom')}
                    onChange={(next) => {
                      if (!next) return
                      setStart(next)
                      void persist({ ...current, start: next })
                    }}
                  />
                  <Typography.Text type="secondary">→</Typography.Text>
                  <TimePicker
                    format={TIME_FORMAT}
                    minuteStep={15}
                    value={end}
                    allowClear={false}
                    aria-label={t('notifications.quietHoursTo')}
                    onChange={(next) => {
                      if (!next) return
                      setEnd(next)
                      void persist({ ...current, end: next })
                    }}
                  />
                </Space>
              }
            />
            <SettingRow
              label={t('notifications.quietHoursBehaviour')}
              control={
                <Select<QuietHoursBehaviour>
                  style={{ width: 200 }}
                  aria-label={t('notifications.quietHoursBehaviour')}
                  value={behaviour}
                  onChange={(next) => {
                    setBehaviour(next)
                    void persist({ ...current, behaviour: next })
                  }}
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
      {invalid && <Alert type="error" showIcon title={t('notifications.quietHoursInvalid')} />}
    </div>
  )
}

export function NotificationSettingsForm() {
  const { t } = useTranslation()
  const { data: settings } = useNotificationSettings()

  return (
    <div className="flex flex-col gap-6">
      <ChannelsSection />
      <PreferencesSection />
      {settings ? (
        <QuietHours settings={settings} />
      ) : (
        <SettingsSection title={t('notifications.quietHoursTitle')} bodyClassName="p-6">
          <Spin />
        </SettingsSection>
      )}
    </div>
  )
}
