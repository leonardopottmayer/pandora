import { Card, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/features/identity/context/auth-context'

export function HomePage() {
  const { t } = useTranslation()
  const { user, loginLabel } = useAuth()
  const name = user?.name ?? loginLabel ?? t('home.defaultUser')

  return (
    <div className="mx-auto max-w-2xl">
      <Card>
        <Typography.Title level={3} className="!mb-1">
          {t('home.greeting', { name })}
        </Typography.Title>
        <Typography.Paragraph type="secondary" className="!mb-0">
          {t('home.welcome')}
        </Typography.Paragraph>
      </Card>
    </div>
  )
}
