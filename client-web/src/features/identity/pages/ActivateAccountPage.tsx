import { useSearchParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button, Card, Result, Spin } from 'antd'
import { useQuery } from '@tanstack/react-query'
import * as authService from '../services/auth.service'
import { AuthLayout } from '../components/AuthLayout'
import { toErrorMessage } from '@/lib/api/envelope'

export function ActivateAccountPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''

  // useQuery (instead of mutate in useEffect) so react-query dedupes the
  // call and shares the result across mounts — avoids the endless spinner
  // caused by StrictMode's double-mount. The activation token is single-use,
  // hence retry: false and no refetch.
  const query = useQuery({
    queryKey: ['activate-account', token],
    queryFn: async () => {
      await authService.activateAccount(token)
      return true as const
    },
    enabled: token !== '',
    retry: false,
    staleTime: Infinity,
    gcTime: Infinity,
    refetchOnMount: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })

  const goLogin = (
    <Link to="/login">
      <Button type="primary">{t('activate.goLogin')}</Button>
    </Link>
  )

  return (
    <AuthLayout>
      <Card className="shadow-sm">
        {!token ? (
          <Result status="error" title={t('activate.invalidTitle')} subTitle={t('activate.invalidDesc')} extra={goLogin} />
        ) : query.isSuccess ? (
          <Result status="success" title={t('activate.successTitle')} subTitle={t('activate.successDesc')} extra={goLogin} />
        ) : query.isError ? (
          <Result
            status="error"
            title={t('activate.errorTitle')}
            subTitle={toErrorMessage(query.error, t('activate.errorDesc'))}
            extra={goLogin}
          />
        ) : (
          <div className="py-8 text-center">
            <Spin size="large" />
            <p className="mt-4">{t('activate.activating')}</p>
          </div>
        )}
      </Card>
    </AuthLayout>
  )
}
