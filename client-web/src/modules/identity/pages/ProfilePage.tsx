import { Alert, Card, Descriptions } from 'antd'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../context/auth-context'

export function ProfilePage() {
  const { t } = useTranslation()
  const { user, loginLabel } = useAuth()

  return (
    <div className="mx-auto max-w-2xl">
      <Card title={t('profile.title')}>
        {user ? (
          <Descriptions column={1} bordered size="small">
            <Descriptions.Item label={t('profile.name')}>{user.name}</Descriptions.Item>
            <Descriptions.Item label={t('profile.email')}>{user.email}</Descriptions.Item>
            <Descriptions.Item label={t('profile.username')}>{user.username}</Descriptions.Item>
          </Descriptions>
        ) : (
          <Alert
            type="info"
            showIcon
            message={t('profile.unavailableTitle')}
            description={t('profile.unavailableDesc', { label: loginLabel ?? '—' })}
          />
        )}
      </Card>
    </div>
  )
}
