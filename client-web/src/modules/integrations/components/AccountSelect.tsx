import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Select, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useAccounts, useStartConnection } from '../hooks/useIntegrations'
import {
  type AccountRequirement,
  candidateAccounts,
  hasScopes,
  preselectedAccount,
} from '../lib/accountSelection'

interface AccountSelectProps extends AccountRequirement {
  value: string | null
  onChange: (accountId: string | null) => void
  /** Where the browser comes back to after a "grant access" consent. */
  redirectAfter: string
  className?: string
}

/**
 * "This feature uses that account": a picker over the user's connected accounts of the accepted
 * providers. The only eligible account is preselected; with several the user chooses (there is no
 * default account). OAuth accounts missing a required scope get a "grant access" action.
 */
export function AccountSelect({ value, onChange, redirectAfter, className, ...requirement }: AccountSelectProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data: accounts, isLoading } = useAccounts()
  const startConnection = useStartConnection()

  const candidates = candidateAccounts(accounts ?? [], requirement)
  const preselected = preselectedAccount(accounts ?? [], requirement)
  const dangling = value !== null && !isLoading && !candidates.some((a) => a.id === value)

  useEffect(() => {
    if (value === null && preselected !== null) onChange(preselected)
  }, [value, preselected, onChange])

  const needingAccess = candidates.filter(
    (a) => a.authKind === 'oauth' && !hasScopes(a, requirement.requiredScopes),
  )

  async function grantAccess(accountId: string, provider: string) {
    try {
      const url = await startConnection.mutateAsync({
        provider,
        redirectAfter,
        accountId,
        scopes: requirement.requiredScopes,
      })
      window.location.assign(url)
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
    }
  }

  return (
    <div className={`flex flex-col gap-1.5 ${className ?? ''}`}>
      <Select
        className="w-full"
        loading={isLoading}
        value={dangling ? null : value}
        onChange={(id: string | undefined) => onChange(id ?? null)}
        allowClear
        placeholder={t('connections.select.placeholder')}
        notFoundContent={t('connections.select.none')}
        options={candidates.map((a) => {
          const usable = a.status === 'connected' && hasScopes(a, requirement.requiredScopes)
          const hint = !hasScopes(a, requirement.requiredScopes)
            ? t('connections.select.missingScopes')
            : a.status !== 'connected'
              ? t('connections.select.unavailable')
              : a.displayName && a.displayName !== a.label
                ? a.displayName
                : null
          return {
            value: a.id,
            disabled: !usable,
            label: hint ? `${a.label} (${hint})` : a.label,
          }
        })}
      />
      {dangling && <Alert type="warning" showIcon message={t('connections.select.dangling')} />}
      {needingAccess.map((a) => (
        <Button
          key={a.id}
          size="small"
          type="link"
          className="w-fit px-0"
          loading={startConnection.isPending}
          onClick={() => grantAccess(a.id, a.provider)}
        >
          {t('connections.select.grantAccess', { label: a.label })}
        </Button>
      ))}
      <Typography.Text type="secondary" className="text-xs">
        <Link to="/integrations/connections">{t('connections.select.connect')}</Link>
      </Typography.Text>
    </div>
  )
}
