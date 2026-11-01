import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Empty, Select, Space, Spin, Switch, Tag, Typography, theme } from 'antd'
import { ArrowDownOutlined, ArrowUpOutlined, DeleteOutlined, EditOutlined, HolderOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { ApiResponseError, toErrorMessage } from '@/lib/api/envelope'
import { useProviders, useStartConnection } from '@/modules/integrations/hooks/useIntegrations'
import { GMAIL_FILTER_SCOPE, GMAIL_SCOPE, type MailRule } from '../models'
import {
  useCreateRule,
  useDeleteRule,
  useLabels,
  useMailboxes,
  useReorderRules,
  useRules,
  useUpdateRule,
} from '../hooks/useMail'
import { RuleFormModal } from '../components/RuleFormModal'
import { draftFrom, needsLabel, newDraft, saveRuleForm, takeRuleForm, toRuleInput, type RuleDraft } from '../lib/ruleForm'

const MISSING_FILTER_SCOPE = 'Communications.MissingFilterScope'

interface OpenForm {
  ruleId: string | null
  draft: RuleDraft
}

export function MailRulesPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()

  // Back from a consent: the form as it was, once.
  const [restored] = useState(takeRuleForm)
  const [chosenId, setChosenId] = useState<string | null>(restored?.mailboxId ?? null)
  const [form, setForm] = useState<OpenForm | null>(restored && { ruleId: restored.ruleId, draft: restored.draft })
  const [needsConsent, setNeedsConsent] = useState(false)
  const [dragging, setDragging] = useState<number | null>(null)

  const { data: mailboxes, isLoading } = useMailboxes()
  const { data: providers } = useProviders()
  const mailbox = mailboxes?.find((m) => m.id === chosenId) ?? mailboxes?.[0] ?? null
  const { data: rules, isLoading: loadingRules } = useRules(mailbox?.id ?? null)
  const labels = useLabels(mailbox ? [mailbox.id] : [])[0]?.data ?? []

  const createRule = useCreateRule()
  const updateRule = useUpdateRule()
  const deleteRule = useDeleteRule()
  const reorder = useReorderRules()
  const startConnection = useStartConnection()

  const outcome = searchParams.get('integration')
  useEffect(() => {
    if (outcome === 'error') message.error(t('connections.connectError'))
    else if (outcome === 'other-account') message.warning(t('connections.otherAccount'), 8)
    if (searchParams.size > 0) setSearchParams({}, { replace: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [outcome])

  /** The one failure with something to do about it: the rest is the server's message. */
  function showError(err: unknown, fallback: string) {
    if (err instanceof ApiResponseError && err.code === MISSING_FILTER_SCOPE) setNeedsConsent(true)
    else message.error(toErrorMessage(err, fallback))
  }

  async function grantAccess(draft: RuleDraft | null) {
    if (!mailbox) return
    try {
      const google = providers?.find((p) => p.provider === 'google')
      const url = await startConnection.mutateAsync({
        provider: 'google',
        redirectAfter: '/mail/rules',
        accountId: mailbox.externalAccountId,
        scopes: [...(google?.defaultScopes ?? []), GMAIL_SCOPE, GMAIL_FILTER_SCOPE],
      })
      if (draft) saveRuleForm({ mailboxId: mailbox.id, ruleId: form?.ruleId ?? null, draft })
      window.location.assign(url)
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
    }
  }

  async function save(draft: RuleDraft) {
    if (!mailbox || !form) return
    const input = toRuleInput(draft)
    const current = rules?.find((r) => r.id === form.ruleId)
    try {
      if (current) await updateRule.mutateAsync({ id: current.id, input: { ...input, isEnabled: current.isEnabled } })
      else await createRule.mutateAsync({ mailboxId: mailbox.id, input })
      setForm(null)
      setNeedsConsent(false)
    } catch (err) {
      showError(err, t('mail.rules.saveError'))
    }
  }

  async function setEnabled(rule: MailRule, isEnabled: boolean) {
    try {
      await updateRule.mutateAsync({
        id: rule.id,
        input: { name: rule.name, conditions: rule.conditions, actions: rule.actions, isEnabled },
      })
    } catch (err) {
      showError(err, t('mail.rules.saveError'))
    }
  }

  function toggle(rule: MailRule, isEnabled: boolean) {
    if (isEnabled || rule.execution !== 'provider-filter') {
      void setEnabled(rule, isEnabled)
      return
    }
    modal.confirm({
      title: t('mail.rules.disableTitle', { name: rule.name }),
      content: t('mail.rules.disableDesc'),
      okText: t('mail.rules.disableOk'),
      okButtonProps: { danger: true },
      onOk: () => setEnabled(rule, false),
    })
  }

  function deleteDescription(rule: MailRule) {
    if (rule.status === 'read-only') return t('mail.rules.deleteReadOnlyDesc')
    return rule.execution === 'provider-filter' && rule.isEnabled ? t('mail.rules.deleteFilterDesc') : t('mail.rules.deleteDesc')
  }

  function confirmDelete(rule: MailRule) {
    modal.confirm({
      title: t('mail.rules.deleteTitle', { name: rule.name }),
      content: deleteDescription(rule),
      okText: t('mail.rules.delete'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteRule.mutateAsync(rule.id)
        } catch (err) {
          showError(err, t('mail.rules.deleteError'))
        }
      },
    })
  }

  async function move(from: number, to: number) {
    if (!mailbox || !rules || from === to || to < 0 || to >= rules.length) return
    const ids = rules.map((r) => r.id)
    ids.splice(to, 0, ids.splice(from, 1)[0])
    try {
      await reorder.mutateAsync({ mailboxId: mailbox.id, ruleIds: ids })
    } catch (err) {
      message.error(toErrorMessage(err, t('mail.rules.orderError')))
    }
  }

  function actionText(action: MailRule['actions'][number]) {
    const text = t(`mail.rules.action.${action.kind}`)
    return needsLabel(action.kind) ? `${text} ${labels.find((l) => l.id === action.labelId)?.name ?? '?'}` : text
  }

  function renderRule(rule: MailRule, index: number) {
    const readOnly = rule.status === 'read-only'
    const broken = rule.status === 'broken'
    const borderColor = broken ? token.colorError : readOnly ? token.colorWarning : token.colorBorderSecondary

    return (
      <div
        key={rule.id}
        draggable
        onDragStart={() => setDragging(index)}
        onDragEnd={() => setDragging(null)}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault()
          if (dragging !== null) void move(dragging, index)
          setDragging(null)
        }}
        className="flex flex-col gap-2 rounded-xl p-4"
        style={{ border: `1px solid ${borderColor}`, background: token.colorBgContainer, opacity: dragging === index ? 0.5 : 1 }}
      >
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Space size={8} wrap>
            <HolderOutlined style={{ cursor: 'grab', color: token.colorTextTertiary }} />
            <Typography.Text strong>{rule.name}</Typography.Text>
            <Tag>{t(rule.execution === 'provider-filter' ? 'mail.rules.runsGmail' : 'mail.rules.runsPandora')}</Tag>
            {broken && <Tag color="red">{t('mail.rules.broken')}</Tag>}
            {readOnly && <Tag color="gold">{t('mail.rules.readOnly')}</Tag>}
            {!rule.isEnabled && <Tag>{t('mail.rules.disabled')}</Tag>}
          </Space>
          <Space size={4}>
            {!readOnly && (
              <Switch
                size="small"
                aria-label={t('mail.rules.enable')}
                checked={rule.isEnabled}
                onChange={(checked) => toggle(rule, checked)}
              />
            )}
            <Button
              type="text"
              size="small"
              aria-label={t('mail.rules.moveUp')}
              icon={<ArrowUpOutlined />}
              disabled={index === 0}
              onClick={() => move(index, index - 1)}
            />
            <Button
              type="text"
              size="small"
              aria-label={t('mail.rules.moveDown')}
              icon={<ArrowDownOutlined />}
              disabled={index === rules!.length - 1}
              onClick={() => move(index, index + 1)}
            />
            {!readOnly && (
              <Button
                type="text"
                size="small"
                aria-label={t('mail.rules.edit')}
                icon={<EditOutlined />}
                onClick={() => setForm({ ruleId: rule.id, draft: draftFrom(rule, labels) })}
              />
            )}
            <Button
              type="text"
              size="small"
              danger
              aria-label={t('mail.rules.delete')}
              icon={<DeleteOutlined />}
              onClick={() => confirmDelete(rule)}
            />
          </Space>
        </div>

        {readOnly ? (
          <>
            <Typography.Text type="secondary">{t('mail.rules.readOnlyHint')}</Typography.Text>
            {rule.providerFilterRaw && (
              <details>
                <summary className="cursor-pointer text-xs">{t('mail.rules.rawFilter')}</summary>
                <pre className="mt-1 overflow-x-auto text-xs">{rule.providerFilterRaw}</pre>
              </details>
            )}
          </>
        ) : (
          <Typography.Text type="secondary" className="text-xs">
            {rule.conditions
              .map((c) => (c.kind === 'has-attachment' ? t('mail.rules.condition.has-attachment') : `${t(`mail.rules.condition.${c.kind}`)} "${c.value}"`))
              .join(' · ')}
            {' → '}
            {rule.actions.map(actionText).join(' · ')}
          </Typography.Text>
        )}

        {rule.lastError && <Alert type={broken ? 'error' : 'warning'} showIcon message={rule.lastError} />}
      </div>
    )
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('mail.rules.title')} description={t('mail.rules.description')} />
        {mailbox && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setForm({ ruleId: null, draft: newDraft() })}>
            {t('mail.rules.new')}
          </Button>
        )}
      </div>

      {isLoading ? (
        <Spin />
      ) : !mailbox ? (
        <Empty description={t('mail.rules.noMailboxes')} />
      ) : (
        <>
          {mailboxes!.length > 1 && (
            <Select
              className="w-72 max-w-full"
              aria-label={t('mail.rules.mailbox')}
              value={mailbox.id}
              options={mailboxes!.map((m) => ({ value: m.id, label: m.displayName ?? m.address }))}
              onChange={(id: string) => {
                setChosenId(id)
                setNeedsConsent(false)
              }}
            />
          )}

          {mailbox.filterSyncError && (
            <Alert type="warning" showIcon message={t('mail.rules.filterSyncError')} description={mailbox.filterSyncError} />
          )}

          {needsConsent && !form && (
            <Alert
              type="warning"
              showIcon
              message={t('mail.rules.consentTitle')}
              action={
                <Button size="small" type="primary" loading={startConnection.isPending} onClick={() => grantAccess(null)}>
                  {t('mail.rules.consentGrant')}
                </Button>
              }
            />
          )}

          {loadingRules ? (
            <Spin />
          ) : (rules ?? []).length === 0 ? (
            <Empty description={t('mail.rules.none')} />
          ) : (
            <div className="flex flex-col gap-3">{rules!.map(renderRule)}</div>
          )}

          {form && (
            <RuleFormModal
              initial={form.draft}
              isEditing={form.ruleId !== null}
              provider={mailbox.provider}
              labels={labels}
              saving={createRule.isPending || updateRule.isPending}
              needsConsent={needsConsent}
              granting={startConnection.isPending}
              onSave={save}
              onGrant={grantAccess}
              onClose={() => setForm(null)}
            />
          )}
        </>
      )}
    </div>
  )
}
