import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Alert,
  App,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Result,
  Space,
  Spin,
  Statistic,
  Steps,
  Tag,
  Typography,
} from 'antd'
import { SafetyOutlined } from '@ant-design/icons'
import { QRCodeSVG } from 'qrcode.react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as mfaService from '../services/mfa.service'
import { toErrorMessage } from '@/lib/api/envelope'
import type { MfaSetup } from '../models'

const STATUS_KEY = ['mfa-status']

/** List of recovery codes with copy/download actions. */
function RecoveryCodes({ codes }: { codes: string[] }) {
  const { t } = useTranslation()
  const { message } = App.useApp()

  function copy() {
    navigator.clipboard.writeText(codes.join('\n')).then(
      () => message.success(t('security.copied')),
      () => message.error(t('security.copyError')),
    )
  }

  function download() {
    const blob = new Blob([codes.join('\n') + '\n'], { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'pandora-recovery-codes.txt'
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div>
      <Alert
        type="warning"
        showIcon
        className="mb-3"
        message={t('security.recoveryTitle')}
        description={t('security.recoveryDesc')}
      />
      <div className="grid grid-cols-2 gap-2 font-mono text-sm">
        {codes.map((c) => (
          <Tag key={c} className="!m-0 text-center !py-1">
            {c}
          </Tag>
        ))}
      </div>
      <Space className="mt-3">
        <Button onClick={copy}>{t('common.copy')}</Button>
        <Button onClick={download}>{t('common.download')}</Button>
      </Space>
    </div>
  )
}

/** MFA enable wizard: setup -> confirm code -> recovery codes. */
function EnableMfaFlow({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [setup, setSetup] = useState<MfaSetup | null>(null)
  const [codes, setCodes] = useState<string[] | null>(null)
  const [form] = Form.useForm()

  const setupMutation = useMutation({
    mutationFn: mfaService.setupMfa,
    onSuccess: setSetup,
    onError: (err) => message.error(toErrorMessage(err, t('security.startError'))),
  })

  const enableMutation = useMutation({
    mutationFn: (code: string) => mfaService.enableMfa(code),
    onSuccess: (res) => setCodes(res.recoveryCodes),
    onError: (err) => message.error(toErrorMessage(err, t('security.enableError'))),
  })

  const step = codes ? 2 : setup ? 1 : 0

  return (
    <div>
      <Steps
        size="small"
        current={step}
        className="mb-6"
        items={[
          { title: t('security.steps.start') },
          { title: t('security.steps.confirm') },
          { title: t('security.steps.codes') },
        ]}
      />

      {step === 0 && (
        <Space direction="vertical">
          <Typography.Paragraph type="secondary">{t('security.startDesc')}</Typography.Paragraph>
          <Button type="primary" loading={setupMutation.isPending} onClick={() => setupMutation.mutate()}>
            {t('security.startButton')}
          </Button>
        </Space>
      )}

      {step === 1 && setup && (
        <div className="flex flex-col gap-4">
          <Typography.Paragraph type="secondary" className="!mb-0">
            {t('security.scanDesc')}
          </Typography.Paragraph>
          <div className="flex flex-col items-center gap-3">
            <div className="bg-white p-3 rounded-lg">
              <QRCodeSVG value={setup.otpauthUri} size={180} />
            </div>
            <Typography.Text copyable className="font-mono text-xs break-all">
              {setup.secret}
            </Typography.Text>
          </div>
          <Form form={form} layout="vertical" onFinish={(v) => enableMutation.mutate(v.code.trim())}>
            <Form.Item
              name="code"
              label={t('security.codeLabel')}
              rules={[{ required: true, message: t('security.codeRequired') }]}
            >
              <Input placeholder="123456" autoComplete="one-time-code" />
            </Form.Item>
            <Form.Item className="mb-0">
              <Button type="primary" htmlType="submit" loading={enableMutation.isPending}>
                {t('security.confirmEnable')}
              </Button>
            </Form.Item>
          </Form>
        </div>
      )}

      {step === 2 && codes && (
        <div className="flex flex-col gap-4">
          <RecoveryCodes codes={codes} />
          <Button type="primary" onClick={onDone}>
            {t('common.finish')}
          </Button>
        </div>
      )}
    </div>
  )
}

/** Generic modal that asks for password + code (disable / regenerate). */
function PasswordCodeModal({
  open,
  title,
  confirmText,
  danger,
  onCancel,
  onSubmit,
  loading,
}: {
  open: boolean
  title: string
  confirmText: string
  danger?: boolean
  loading: boolean
  onCancel: () => void
  onSubmit: (password: string, code: string) => void
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  return (
    <Modal
      open={open}
      title={title}
      onCancel={onCancel}
      destroyOnHidden
      okText={confirmText}
      okButtonProps={{ danger, loading }}
      onOk={() => form.submit()}
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={(v) => onSubmit(v.password, v.code.trim())}
        className="mt-4"
      >
        <Form.Item
          name="password"
          label={t('security.passwordLabel')}
          rules={[{ required: true, message: t('security.passwordRequired') }]}
        >
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          name="code"
          label={t('security.codeModalLabel')}
          rules={[{ required: true, message: t('security.codeRequired') }]}
        >
          <Input placeholder="123456" autoComplete="one-time-code" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export function SecurityPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [disableOpen, setDisableOpen] = useState(false)
  const [regenOpen, setRegenOpen] = useState(false)
  const [newCodes, setNewCodes] = useState<string[] | null>(null)

  const statusQuery = useQuery({ queryKey: STATUS_KEY, queryFn: mfaService.getMfaStatus })

  const refreshStatus = () => queryClient.invalidateQueries({ queryKey: STATUS_KEY })

  const disableMutation = useMutation({
    mutationFn: ({ password, code }: { password: string; code: string }) =>
      mfaService.disableMfa(password, code),
    onSuccess: () => {
      message.success(t('security.disabled'))
      setDisableOpen(false)
      refreshStatus()
    },
    onError: (err) => message.error(toErrorMessage(err, t('security.disableError'))),
  })

  const regenMutation = useMutation({
    mutationFn: ({ password, code }: { password: string; code: string }) =>
      mfaService.regenerateRecoveryCodes(password, code),
    onSuccess: (res) => {
      message.success(t('security.regenerated'))
      setRegenOpen(false)
      setNewCodes(res.recoveryCodes)
      refreshStatus()
    },
    onError: (err) => message.error(toErrorMessage(err, t('security.regenerateError'))),
  })

  if (statusQuery.isLoading) {
    return (
      <div className="py-12 text-center">
        <Spin size="large" />
      </div>
    )
  }

  if (statusQuery.isError) {
    return (
      <div className="mx-auto max-w-2xl">
        <Result status="error" title={t('security.loadError')} />
      </div>
    )
  }

  const status = statusQuery.data!

  return (
    <div className="mx-auto max-w-2xl">
      <Card
        title={
          <Space>
            <SafetyOutlined />
            {t('security.title')}
          </Space>
        }
      >
        {status.enabled ? (
          <Space direction="vertical" size="large" className="w-full">
            <Space size="large">
              <Tag color="success" className="!text-sm !px-3 !py-1">
                {t('security.enabled')}
              </Tag>
              <Statistic title={t('security.remaining')} value={status.remainingRecoveryCodes} />
            </Space>
            {status.remainingRecoveryCodes <= 2 && (
              <Alert type="warning" showIcon message={t('security.lowCodes')} />
            )}
            <Space wrap>
              <Button onClick={() => setRegenOpen(true)}>{t('security.regenerate')}</Button>
              <Button danger onClick={() => setDisableOpen(true)}>
                {t('security.disable')}
              </Button>
            </Space>
          </Space>
        ) : (
          <EnableMfaFlow onDone={refreshStatus} />
        )}
      </Card>

      <PasswordCodeModal
        open={disableOpen}
        title={t('security.disableTitle')}
        confirmText={t('security.disableConfirm')}
        danger
        loading={disableMutation.isPending}
        onCancel={() => setDisableOpen(false)}
        onSubmit={(password, code) => disableMutation.mutate({ password, code })}
      />

      <PasswordCodeModal
        open={regenOpen}
        title={t('security.regenerateTitle')}
        confirmText={t('security.regenerateConfirm')}
        loading={regenMutation.isPending}
        onCancel={() => setRegenOpen(false)}
        onSubmit={(password, code) => regenMutation.mutate({ password, code })}
      />

      <Modal
        open={newCodes != null}
        title={t('security.newCodesTitle')}
        footer={[
          <Button key="ok" type="primary" onClick={() => setNewCodes(null)}>
            {t('common.finish')}
          </Button>,
        ]}
        onCancel={() => setNewCodes(null)}
      >
        {newCodes && <RecoveryCodes codes={newCodes} />}
      </Modal>
    </div>
  )
}
