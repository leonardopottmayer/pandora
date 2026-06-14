import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Form, Input } from 'antd'
import { useMutation } from '@tanstack/react-query'
import * as authService from '../services/auth.service'
import { AuthLayout } from '../components/AuthLayout'
import { passwordRule } from '../passwordPolicy'
import { toErrorMessage } from '@/lib/api/envelope'
import type { SignUpRequest } from '../models'

export function RegisterPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { message } = App.useApp()

  const mutation = useMutation({
    mutationFn: (values: SignUpRequest) => authService.signUp(values),
    onSuccess: () => {
      message.success(t('register.success'))
      navigate('/login', { replace: true })
    },
    onError: (err) => message.error(toErrorMessage(err, t('register.error'))),
  })

  return (
    <AuthLayout>
      <Card title={t('register.title')} className="shadow-sm">
        <Form layout="vertical" onFinish={mutation.mutate} autoComplete="on">
          <Form.Item name="name" label={t('register.name')} rules={[{ required: true, message: t('register.nameRequired') }]}>
            <Input placeholder={t('register.name')} autoComplete="name" size="large" />
          </Form.Item>
          <Form.Item
            name="email"
            label={t('register.email')}
            rules={[
              { required: true, message: t('register.emailRequired') },
              { type: 'email', message: t('register.emailInvalid') },
            ]}
          >
            <Input placeholder={t('register.email')} type="email" autoComplete="email" size="large" />
          </Form.Item>
          <Form.Item
            name="username"
            label={t('register.username')}
            rules={[{ required: true, message: t('register.usernameRequired') }]}
          >
            <Input placeholder={t('register.username')} autoComplete="username" size="large" />
          </Form.Item>
          <Form.Item name="password" label={t('register.password')} tooltip={t('password.hint')} rules={[passwordRule]}>
            <Input.Password placeholder={t('register.password')} autoComplete="new-password" size="large" />
          </Form.Item>
          <Form.Item className="mb-0">
            <Button type="primary" htmlType="submit" loading={mutation.isPending} block size="large">
              {t('register.submit')}
            </Button>
          </Form.Item>
        </Form>
        <div className="mt-4 text-center">
          <Link to="/login">{t('register.haveAccount')}</Link>
        </div>
      </Card>
    </AuthLayout>
  )
}
