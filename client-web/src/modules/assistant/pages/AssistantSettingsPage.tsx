import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input, Segmented, Select, Spin, Switch } from 'antd'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
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

  if (isLoading) {
    return (
      <div className="mx-auto flex max-w-3xl justify-center py-16">
        <Spin />
      </div>
    )
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeading title={t('assistant.settings.title')} description={t('assistant.settings.intro')} />

      <SettingsSection>
        <SettingRow
          label={t('assistant.settings.enabledLabel')}
          description={t('assistant.settings.enabledDesc')}
          control={<Switch checked={isEnabled} onChange={setIsEnabled} />}
        />
      </SettingsSection>

      <Alert
        type="info"
        showIcon
        message={t('assistant.settings.privacyTitle')}
        description={t('assistant.settings.privacyDesc')}
      />

      <SettingsSection title={t('assistant.settings.groupModel')}>
        <SettingRow
          label={t('assistant.settings.providerLabel')}
          description={
            keyConfigured && selectedProvider?.keyHint
              ? t('assistant.settings.keyHint', { hint: selectedProvider.keyHint })
              : t('assistant.settings.providerDesc')
          }
          control={
            <Select
              className="w-full min-w-56 sm:w-64"
              value={provider}
              onChange={setProvider}
              options={providerOptions.length > 0 ? providerOptions : [{ label: 'Google Gemini', value: 'gemini' }]}
            />
          }
        />
        <SettingRow
          label={t('assistant.settings.modelLabel')}
          description={t('assistant.settings.modelDesc')}
          control={
            <Input
              className="w-full min-w-56 sm:w-64"
              value={model}
              onChange={(e) => setModel(e.target.value)}
              placeholder="gemini-3.6-flash"
            />
          }
        />
        <SettingRow
          label={t('assistant.settings.testLabel')}
          description={t('assistant.settings.testDesc')}
          control={
            <Button loading={testProvider.isPending} onClick={handleTest}>
              {t('assistant.settings.test.button')}
            </Button>
          }
        />
      </SettingsSection>

      {!keyConfigured && (
        <Alert
          type="warning"
          showIcon
          message={t('assistant.settings.noKeyTitle')}
          description={<Link to="/integrations/connections">{t('assistant.settings.noKeyAction')}</Link>}
        />
      )}

      {testResult &&
        (testResult.ok ? (
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
        ))}

      <SettingsSection title={t('assistant.settings.groupBehavior')}>
        <SettingRow
          label={t('assistant.settings.confirmationLabel')}
          description={t('assistant.settings.confirmationDesc')}
          control={
            <Segmented<ConfirmationLevel>
              value={confirmationLevel}
              onChange={setConfirmationLevel}
              options={[
                { label: t('assistant.settings.confirmStrict'), value: 'strict' },
                { label: t('assistant.settings.confirmBalanced'), value: 'balanced' },
                { label: t('assistant.settings.confirmTrusting'), value: 'trusting' },
              ]}
            />
          }
        />
        <SettingRow
          label={t('assistant.settings.localeLabel')}
          description={t('assistant.settings.localeDesc')}
          control={
            <Select
              className="w-full min-w-56 sm:w-64"
              value={localeOverride ?? ''}
              onChange={(v) => setLocaleOverride(v === '' ? null : v)}
              options={[
                { label: t('assistant.settings.localeAccount'), value: '' },
                { label: 'Português (pt-BR)', value: 'pt-BR' },
                { label: 'English (en)', value: 'en' },
              ]}
            />
          }
        />
      </SettingsSection>

      <div>
        <Button type="primary" loading={saveProfile.isPending} onClick={handleSave}>
          {t('assistant.settings.save')}
        </Button>
      </div>
    </div>
  )
}
