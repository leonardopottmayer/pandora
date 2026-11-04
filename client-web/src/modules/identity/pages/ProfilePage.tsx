import { Alert, Avatar, Typography, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { SettingRow } from '@/components/settings/SettingRow'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { useAuth } from '../context/auth-context'

/** First and last name's initials: "Ana Maria Souza" is "AS". */
function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '?'
  const last = words.length > 1 ? words[words.length - 1][0] : ''
  return (words[0][0] + last).toUpperCase()
}

export function ProfilePage() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { user, loginLabel } = useAuth()

  if (!user) {
    return (
      <div className="mx-auto max-w-3xl">
        <Alert
          type="info"
          showIcon
          title={t('profile.unavailableTitle')}
          description={t('profile.unavailableDesc', { label: loginLabel ?? '—' })}
        />
      </div>
    )
  }

  const data = [
    { label: t('profile.name'), value: user.name },
    { label: t('profile.email'), value: user.email },
    { label: t('profile.username'), value: user.username },
  ]

  const shortcuts = [
    { to: '/account/security', label: t('nav.security'), description: t('profile.securityDesc') },
    { to: '/account/password', label: t('nav.changePassword'), description: t('profile.passwordDesc') },
    { to: '/account/devices', label: t('nav.devices'), description: t('profile.devicesDesc') },
  ]

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div className="flex items-center gap-4">
        <Avatar size={64} style={{ background: token.colorPrimary, flexShrink: 0, fontSize: 24 }}>
          {initials(user.name)}
        </Avatar>
        <div className="flex min-w-0 flex-col gap-0.5">
          <Typography.Title level={4} className="!mb-0" ellipsis>
            {user.name}
          </Typography.Title>
          <Typography.Text type="secondary" ellipsis>
            {user.email}
          </Typography.Text>
        </div>
      </div>

      <SettingsSection title={t('profile.groupData')}>
        {data.map(({ label, value }) => (
          <SettingRow key={label} label={label} control={<Typography.Text copyable>{value}</Typography.Text>} />
        ))}
      </SettingsSection>

      <SettingsSection title={t('profile.groupAccount')}>
        {shortcuts.map(({ to, label, description }) => (
          <SettingRow
            key={to}
            label={label}
            description={description}
            control={
              <Link to={to} aria-label={label}>
                {t('profile.open')}
              </Link>
            }
          />
        ))}
      </SettingsSection>
    </div>
  )
}
