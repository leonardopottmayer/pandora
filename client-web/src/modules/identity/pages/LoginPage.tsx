import { Link, useNavigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Form, Input } from 'antd'
import { useMutation } from '@tanstack/react-query'
import { useAuth } from '../context/auth-context'
import { AuthLayout } from '../components/AuthLayout'
import { toErrorMessage } from '@/lib/api/envelope'

interface LoginForm {
  login: string
  password: string
}

export function LoginPage() {
  const { t } = useTranslation()
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { message } = App.useApp()

  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/'

  const mutation = useMutation({
    mutationFn: (values: LoginForm) => login(values.login.trim(), values.password),
    onSuccess: (result) => {
      if (result.mfa) {
        navigate('/mfa', { state: { ticket: result.mfa.ticket, from }, replace: true })
        return
      }
      message.success(t('login.success'))
      navigate(from, { replace: true })
    },
    onError: (err) => message.error(toErrorMessage(err, t('login.error'))),
  })

  return (
    <AuthLayout>
      <Card title={t('login.title')} className="shadow-sm">
        <Form layout="vertical" onFinish={mutation.mutate} autoComplete="on">
          <Form.Item
            name="login"
            label={t('login.idLabel')}
            rules={[{ required: true, message: t('login.idRequired') }]}
          >
            <Input placeholder={t('login.idLabel')} autoComplete="username" size="large" />
          </Form.Item>
          <Form.Item
            name="password"
            label={t('login.passwordLabel')}
            rules={[{ required: true, message: t('login.passwordRequired') }]}
          >
            <Input.Password
              placeholder={t('login.passwordLabel')}
              autoComplete="current-password"
              size="large"
            />
          </Form.Item>
          <Form.Item className="mb-0">
            <Button type="primary" htmlType="submit" loading={mutation.isPending} block size="large">
              {t('login.submit')}
            </Button>
          </Form.Item>
        </Form>
        <div className="mt-4 flex flex-col items-center gap-2 text-center">
          <Link to="/forgot-password">{t('login.forgot')}</Link>
          <Link to="/register">{t('login.noAccount')}</Link>
        </div>
      </Card>
    </AuthLayout>
  )
}
