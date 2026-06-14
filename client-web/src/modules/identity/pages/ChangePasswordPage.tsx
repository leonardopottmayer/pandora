import { useTranslation } from 'react-i18next'
import { App, Button, Card, Form, Input } from 'antd'
import { useMutation } from '@tanstack/react-query'
import * as authService from '../services/auth.service'
import { passwordRule } from '../passwordPolicy'
import { toErrorMessage } from '@/lib/api/envelope'

interface ChangePasswordForm {
  currentPassword: string
  newPassword: string
}

export function ChangePasswordPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm()

  const mutation = useMutation({
    mutationFn: (values: ChangePasswordForm) =>
      authService.changePassword(values.currentPassword, values.newPassword),
    onSuccess: () => {
      message.success(t('password.success'))
      form.resetFields()
    },
    onError: (err) => message.error(toErrorMessage(err, t('password.error'))),
  })

  return (
    <div className="mx-auto max-w-md">
      <Card title={t('password.title')}>
        <Form form={form} layout="vertical" onFinish={mutation.mutate}>
          <Form.Item
            name="currentPassword"
            label={t('password.current')}
            rules={[{ required: true, message: t('password.currentRequired') }]}
          >
            <Input.Password placeholder={t('password.current')} autoComplete="current-password" size="large" />
          </Form.Item>
          <Form.Item name="newPassword" label={t('password.new')} tooltip={t('password.hint')} rules={[passwordRule]}>
            <Input.Password placeholder={t('password.new')} autoComplete="new-password" size="large" />
          </Form.Item>
          <Form.Item
            name="confirm"
            label={t('password.confirm')}
            dependencies={['newPassword']}
            rules={[
              { required: true, message: t('password.confirmRequired') },
              ({ getFieldValue }) => ({
                validator(_r, value) {
                  if (!value || getFieldValue('newPassword') === value) return Promise.resolve()
                  return Promise.reject(new Error(t('password.mismatch')))
                },
              }),
            ]}
          >
            <Input.Password placeholder={t('password.confirm')} autoComplete="new-password" size="large" />
          </Form.Item>
          <Form.Item className="mb-0">
            <Button type="primary" htmlType="submit" loading={mutation.isPending} size="large">
              {t('password.submit')}
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </div>
  )
}
