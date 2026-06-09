import { useSearchParams, Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Form, Input, Alert } from 'antd'
import { useMutation } from '@tanstack/react-query'
import * as authService from '../services/auth.service'
import { AuthLayout } from '../components/AuthLayout'
import { passwordRule } from '../passwordPolicy'
import { toErrorMessage } from '@/lib/api/envelope'

export function ResetPasswordPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const { message } = App.useApp()

  const mutation = useMutation({
    mutationFn: (values: { password: string }) => authService.resetPassword(token, values.password),
    onSuccess: () => {
      message.success(t('reset.success'))
      navigate('/login', { replace: true })
    },
    onError: (err) => message.error(toErrorMessage(err, t('reset.error'))),
  })

  return (
    <AuthLayout>
      <Card title={t('reset.title')} className="shadow-sm">
        {!token ? (
          <Alert
            type="error"
            showIcon
            message={t('reset.invalidTitle')}
            description={t('reset.invalidDesc')}
            action={<Link to="/forgot-password">{t('reset.requestNew')}</Link>}
          />
        ) : (
          <>
            <Form layout="vertical" onFinish={mutation.mutate}>
              <Form.Item
                name="password"
                label={t('reset.newPassword')}
                tooltip={t('password.hint')}
                rules={[passwordRule]}
              >
                <Input.Password placeholder={t('reset.newPassword')} autoComplete="new-password" size="large" />
              </Form.Item>
              <Form.Item
                name="confirm"
                label={t('reset.confirm')}
                dependencies={['password']}
                rules={[
                  { required: true, message: t('reset.confirmRequired') },
                  ({ getFieldValue }) => ({
                    validator(_r, value) {
                      if (!value || getFieldValue('password') === value) return Promise.resolve()
                      return Promise.reject(new Error(t('reset.mismatch')))
                    },
                  }),
                ]}
              >
                <Input.Password placeholder={t('reset.confirm')} autoComplete="new-password" size="large" />
              </Form.Item>
              <Form.Item className="mb-0">
                <Button type="primary" htmlType="submit" loading={mutation.isPending} block size="large">
                  {t('reset.submit')}
                </Button>
              </Form.Item>
            </Form>
            <div className="mt-4 text-center">
              <Link to="/login">{t('common.backToLogin')}</Link>
            </div>
          </>
        )}
      </Card>
    </AuthLayout>
  )
}
