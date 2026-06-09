import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Form, Input, Result } from 'antd'
import { useMutation } from '@tanstack/react-query'
import * as authService from '../services/auth.service'
import { AuthLayout } from '../components/AuthLayout'
import { toErrorMessage } from '@/lib/api/envelope'

export function ForgotPasswordPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [sent, setSent] = useState(false)

  const mutation = useMutation({
    mutationFn: (values: { email: string }) => authService.forgotPassword(values.email.trim()),
    onSuccess: () => setSent(true),
    onError: (err) => message.error(toErrorMessage(err, t('forgot.error'))),
  })

  return (
    <AuthLayout>
      {sent ? (
        <Card className="shadow-sm">
          <Result
            status="success"
            title={t('forgot.sentTitle')}
            subTitle={t('forgot.sentDesc')}
            extra={
              <Link to="/login">
                <Button type="primary">{t('common.backToLogin')}</Button>
              </Link>
            }
          />
        </Card>
      ) : (
        <Card title={t('forgot.title')} className="shadow-sm">
          <Form layout="vertical" onFinish={mutation.mutate}>
            <Form.Item
              name="email"
              label={t('forgot.email')}
              rules={[
                { required: true, message: t('forgot.emailRequired') },
                { type: 'email', message: t('forgot.emailInvalid') },
              ]}
            >
              <Input placeholder={t('forgot.email')} type="email" autoComplete="email" size="large" />
            </Form.Item>
            <Form.Item className="mb-0">
              <Button type="primary" htmlType="submit" loading={mutation.isPending} block size="large">
                {t('forgot.submit')}
              </Button>
            </Form.Item>
          </Form>
          <div className="mt-4 text-center">
            <Link to="/login">{t('common.backToLogin')}</Link>
          </div>
        </Card>
      )}
    </AuthLayout>
  )
}
