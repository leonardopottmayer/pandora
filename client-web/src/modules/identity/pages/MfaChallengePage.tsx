import { useNavigate, useLocation, Navigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Form, Input, Typography } from 'antd'
import { useMutation } from '@tanstack/react-query'
import { useAuth } from '../context/auth-context'
import { AuthLayout } from '../components/AuthLayout'
import { toErrorMessage } from '@/lib/api/envelope'

export function MfaChallengePage() {
  const { t } = useTranslation()
  const { completeMfa } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { message } = App.useApp()

  const state = location.state as { ticket?: string; from?: string } | null
  const ticket = state?.ticket
  const from = state?.from ?? '/'

  const mutation = useMutation({
    mutationFn: (values: { code: string }) => completeMfa(ticket!, values.code.trim()),
    onSuccess: () => {
      message.success(t('mfa.success'))
      navigate(from, { replace: true })
    },
    onError: (err) => message.error(toErrorMessage(err, t('mfa.error'))),
  })

  if (!ticket) return <Navigate to="/login" replace />

  return (
    <AuthLayout>
      <Card title={t('mfa.title')} className="shadow-sm">
        <Typography.Paragraph type="secondary">{t('mfa.desc')}</Typography.Paragraph>
        <Form layout="vertical" onFinish={mutation.mutate}>
          <Form.Item
            name="code"
            label={t('mfa.codeLabel')}
            rules={[{ required: true, message: t('mfa.codeRequired') }]}
          >
            <Input placeholder="123456" size="large" autoFocus autoComplete="one-time-code" />
          </Form.Item>
          <Form.Item className="mb-0">
            <Button type="primary" htmlType="submit" loading={mutation.isPending} block size="large">
              {t('mfa.verify')}
            </Button>
          </Form.Item>
        </Form>
        <div className="mt-4 text-center">
          <Link to="/login">{t('common.backToLogin')}</Link>
        </div>
      </Card>
    </AuthLayout>
  )
}
