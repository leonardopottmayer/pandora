import { Button, Result } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiResponseError, toErrorMessage } from '@/lib/api/envelope'
import { MFA_REQUIRED } from '../models'

/**
 * Why a panel screen could not load. The usual case is an administrator without two-step
 * verification, which the panel requires: they are sent to turn it on.
 */
export function AdminError({ error }: { error: unknown }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  if (error instanceof ApiResponseError && error.code === MFA_REQUIRED) {
    return (
      <Result
        status="warning"
        title={t('admin.mfaRequired.title')}
        subTitle={t('admin.mfaRequired.description')}
        extra={
          <Button type="primary" onClick={() => navigate('/account/security')}>
            {t('admin.mfaRequired.action')}
          </Button>
        }
      />
    )
  }

  return <Result status="error" title={t('admin.loadError')} subTitle={toErrorMessage(error, '')} />
}
