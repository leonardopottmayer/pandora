import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Card, Divider, Input, Segmented, Select, Space, Spin, Switch, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { ConfirmationLevel, ReachabilityResult } from '../models'
import {
  useAssistantProfile,
  useAssistantProviders,
  useSaveAssistantProfile,
  useTestProvider,
} from '../hooks/useAssistant'

export function AssistantSettingsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()

  const { data: profile, isLoading } = useAssistantProfile()
  const { data: providers } = useAssistantProviders()
  const saveProfile = useSaveAssistantProfile()
  const testProvider = useTestProvider()

  const [provider, setProvider] = useState('gemini')
  const [model, setModel] = useState('')
  const [isEnabled, setIsEnabled] = useState(false)
  const [localeOverride, setLocaleOverride] = useState<string | null>(null)
  const [confirmationLevel, setConfirmationLevel] = useState<ConfirmationLevel>('balanced')
  const [testResult, setTestResult] = useState<ReachabilityResult | null>(null)

  // Seed the form once the saved profile (or its defaults) arrives.
  useEffect(() => {
    if (!profile) return
    setProvider(profile.provider)
    setModel(profile.model)
    setIsEnabled(profile.isEnabled)
    setLocaleOverride(profile.localeOverride)
    setConfirmationLevel(profile.confirmationLevel)
  }, [profile])

  const selectedProvider = (providers ?? []).find((p) => p.provider === provider)
  const keyConfigured = selectedProvider?.keyConfigured ?? false

  async function handleSave() {
    if (!model.trim()) {
      message.error(t('assistant.settings.modelRequired'))
      return
    }
    try {
      await saveProfile.mutateAsync({
        provider,
        model: model.trim(),
        isEnabled,
        localeOverride,
        confirmationLevel,
      })
      message.success(t('assistant.settings.saved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.settings.saveError')))
    }
  }

  async function handleTest() {
    setTestResult(null)
    try {
      const result = await testProvider.mutateAsync({ provider, model: model.trim() || undefined })
      setTestResult(result)
    } catch (err) {
      message.error(toErrorMessage(err, t('assistant.settings.test.error')))
    }
  }

  const providerOptions = (providers ?? []).map((p) => ({ label: p.displayName, value: p.provider }))

  return (
    <div className="mx-auto max-w-2xl">
      <Card title={t('assistant.settings.title')} loading={isLoading}>
        <Typography.Paragraph type="secondary">{t('assistant.settings.intro')}</Typography.Paragraph>

        <Alert
          type="info"
          showIcon
          className="mb-4"
          message={t('assistant.settings.privacyTitle')}
          description={t('assistant.settings.privacyDesc')}
        />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('assistant.settings.providerLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('assistant.settings.providerDesc')}</Typography.Text>
          <Select
            className="mt-2 w-full max-w-xs"
            value={provider}
            onChange={setProvider}
            options={providerOptions.length > 0 ? providerOptions : [{ label: 'Google Gemini', value: 'gemini' }]}
          />
          {!keyConfigured && (
            <Alert
              type="warning"
              showIcon
              className="mt-2"
              message={t('assistant.settings.noKeyTitle')}
              description={
                <Link to="/settings/connections">{t('assistant.settings.noKeyAction')}</Link>
              }
            />
          )}
          {keyConfigured && selectedProvider?.keyHint && (
            <Typography.Text type="secondary" className="text-xs">
              {t('assistant.settings.keyHint', { hint: selectedProvider.keyHint })}
            </Typography.Text>
          )}
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('assistant.settings.modelLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('assistant.settings.modelDesc')}</Typography.Text>
          <Input
            className="mt-2 w-full max-w-xs"
            value={model}
            onChange={(e) => setModel(e.target.value)}
            placeholder="gemini-3.6-flash"
          />
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('assistant.settings.confirmationLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('assistant.settings.confirmationDesc')}</Typography.Text>
          <Segmented<ConfirmationLevel>
            className="mt-2 w-fit"
            value={confirmationLevel}
            onChange={setConfirmationLevel}
            options={[
              { label: t('assistant.settings.confirmStrict'), value: 'strict' },
              { label: t('assistant.settings.confirmBalanced'), value: 'balanced' },
              { label: t('assistant.settings.confirmTrusting'), value: 'trusting' },
            ]}
          />
        </div>

        <Divider />

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('assistant.settings.localeLabel')}</Typography.Text>
          <Typography.Text type="secondary">{t('assistant.settings.localeDesc')}</Typography.Text>
          <Select
            className="mt-2 w-full max-w-xs"
            value={localeOverride ?? ''}
            onChange={(v) => setLocaleOverride(v === '' ? null : v)}
            options={[
              { label: t('assistant.settings.localeAccount'), value: '' },
              { label: 'Português (pt-BR)', value: 'pt-BR' },
              { label: 'English (en)', value: 'en' },
            ]}
          />
        </div>

        <Divider />

        <div className="flex items-center gap-3">
          <Switch checked={isEnabled} onChange={setIsEnabled} />
          <div className="flex flex-col">
            <Typography.Text strong>{t('assistant.settings.enabledLabel')}</Typography.Text>
            <Typography.Text type="secondary" className="text-xs">
              {t('assistant.settings.enabledDesc')}
            </Typography.Text>
          </div>
        </div>

        <Divider />

        <Space>
          <Button type="primary" loading={saveProfile.isPending} onClick={handleSave}>
            {t('assistant.settings.save')}
          </Button>
          <Button loading={testProvider.isPending} onClick={handleTest}>
            {t('assistant.settings.test.button')}
          </Button>
          {testProvider.isPending && <Spin size="small" />}
        </Space>

        {testResult && (
          <div className="mt-4">
            {testResult.ok ? (
              <Alert
                type="success"
                showIcon
                message={t('assistant.settings.test.ok', { latency: testResult.latencyMs })}
                description={testResult.reply ?? undefined}
              />
            ) : (
              <Alert
                type={testResult.errorKind === 'unreachable' ? 'warning' : 'error'}
                showIcon
                message={t(`assistant.settings.test.kind.${testResult.errorKind ?? 'rejected'}`)}
                description={testResult.error ?? undefined}
              />
            )}
          </div>
        )}
      </Card>
    </div>
  )
}
