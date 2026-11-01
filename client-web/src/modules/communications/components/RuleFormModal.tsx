import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Input, Modal, Select, Typography } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import type { MailLabel, MailProvider } from '../models'
import {
  actionKinds,
  conditionKinds,
  needsLabel,
  newAction,
  newCondition,
  ruleLabels,
  validateRule,
  type RuleDraft,
} from '../lib/ruleForm'

interface RuleFormModalProps {
  /** Mounted when the form opens: this is where it starts from. */
  initial: RuleDraft
  isEditing: boolean
  provider: MailProvider
  labels: MailLabel[]
  saving: boolean
  needsConsent: boolean
  granting: boolean
  onSave: (draft: RuleDraft) => void
  onGrant: (draft: RuleDraft) => void
  onClose: () => void
}

export function RuleFormModal({
  initial,
  isEditing,
  provider,
  labels,
  saving,
  needsConsent,
  granting,
  onSave,
  onClose,
  onGrant,
}: RuleFormModalProps) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState(initial)
  const [error, setError] = useState<string | null>(null)

  const labelOptions = ruleLabels(labels, provider).map((l) => ({ value: l.id, label: l.name }))

  function save() {
    const problem = validateRule(draft, provider)
    setError(problem)
    if (!problem) onSave(draft)
  }

  const setCondition = (index: number, change: Partial<RuleDraft['conditions'][number]>) =>
    setDraft({ ...draft, conditions: draft.conditions.map((c, i) => (i === index ? { ...c, ...change } : c)) })

  const setAction = (index: number, change: Partial<RuleDraft['actions'][number]>) =>
    setDraft({ ...draft, actions: draft.actions.map((a, i) => (i === index ? { ...a, ...change } : a)) })

  return (
    <Modal
      open
      title={t(isEditing ? 'mail.rules.form.editTitle' : 'mail.rules.form.newTitle')}
      okText={t('mail.rules.form.save')}
      confirmLoading={saving}
      onOk={save}
      onCancel={onClose}
      width={640}
    >
      <div className="flex flex-col gap-4">
        {needsConsent && (
          <Alert
            type="warning"
            showIcon
            message={t('mail.rules.consentTitle')}
            description={t('mail.rules.consentDesc')}
            action={
              <Button size="small" type="primary" loading={granting} onClick={() => onGrant(draft)}>
                {t('mail.rules.consentGrant')}
              </Button>
            }
          />
        )}

        <label className="flex flex-col gap-1">
          <Typography.Text strong>{t('mail.rules.form.name')}</Typography.Text>
          <Input value={draft.name} maxLength={100} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
        </label>

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('mail.rules.form.when')}</Typography.Text>
          {draft.conditions.map((condition, index) => (
            <div key={index} className="flex items-center gap-2">
              <Select
                className="w-52 shrink-0"
                aria-label={t('mail.rules.form.when')}
                value={condition.kind}
                options={conditionKinds(provider).map((kind) => ({ value: kind, label: t(`mail.rules.condition.${kind}`) }))}
                onChange={(kind) => setCondition(index, { kind })}
              />
              {condition.kind !== 'has-attachment' && (
                <Input
                  aria-label={t('mail.rules.form.value')}
                  placeholder={t('mail.rules.form.value')}
                  value={condition.value}
                  onChange={(e) => setCondition(index, { value: e.target.value })}
                />
              )}
              <Button
                type="text"
                aria-label={t('mail.rules.form.remove')}
                icon={<DeleteOutlined />}
                onClick={() => setDraft({ ...draft, conditions: draft.conditions.filter((_, i) => i !== index) })}
              />
            </div>
          ))}
          <div>
            <Button
              size="small"
              icon={<PlusOutlined />}
              onClick={() => setDraft({ ...draft, conditions: [...draft.conditions, newCondition()] })}
            >
              {t('mail.rules.form.addCondition')}
            </Button>
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('mail.rules.form.then')}</Typography.Text>
          {draft.actions.map((action, index) => (
            <div key={index} className="flex items-center gap-2">
              <Select
                className="w-52 shrink-0"
                aria-label={t('mail.rules.form.then')}
                value={action.kind}
                options={actionKinds(provider).map((kind) => ({ value: kind, label: t(`mail.rules.action.${kind}`) }))}
                onChange={(kind) => setAction(index, { kind })}
              />
              {needsLabel(action.kind) && (
                <Select
                  className="min-w-0 flex-1"
                  aria-label={t(provider === 'gmail' ? 'mail.rules.form.label' : 'mail.rules.form.folder')}
                  placeholder={t(provider === 'gmail' ? 'mail.rules.form.label' : 'mail.rules.form.folder')}
                  value={action.labelId ?? undefined}
                  options={labelOptions}
                  onChange={(labelId) => setAction(index, { labelId })}
                />
              )}
              <Button
                type="text"
                aria-label={t('mail.rules.form.remove')}
                icon={<DeleteOutlined />}
                onClick={() => setDraft({ ...draft, actions: draft.actions.filter((_, i) => i !== index) })}
              />
            </div>
          ))}
          <div>
            <Button
              size="small"
              icon={<PlusOutlined />}
              onClick={() => setDraft({ ...draft, actions: [...draft.actions, newAction()] })}
            >
              {t('mail.rules.form.addAction')}
            </Button>
          </div>
        </div>

        {error && <Alert type="error" showIcon message={t(`mail.rules.error.${error}`)} />}
      </div>
    </Modal>
  )
}
