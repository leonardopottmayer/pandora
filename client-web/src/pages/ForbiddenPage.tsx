import { Button, Result } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

/** A screen the user holds no permission for. */
export function ForbiddenPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <Result
      status="403"
      title="403"
      subTitle={t('forbidden.subtitle')}
      extra={
        <Button type="primary" onClick={() => navigate('/')}>
          {t('notFound.back')}
        </Button>
      }
    />
  )
}
