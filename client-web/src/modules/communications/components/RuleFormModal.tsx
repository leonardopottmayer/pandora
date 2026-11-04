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
      cancelText={t('common.cancel')}
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      <div className="flex flex-col gap-4">
        {needsConsent && (
          <Alert
            type="warning"
            showIcon
            title={t('mail.rules.consentTitle')}
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
            <div key={index} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-2 sm:flex">
              <Select
                className="col-start-1 w-full min-w-0 sm:w-52 sm:shrink-0"
                aria-label={t('mail.rules.form.when')}
                value={condition.kind}
                options={conditionKinds(provider).map((kind) => ({ value: kind, label: t(`mail.rules.condition.${kind}`) }))}
                onChange={(kind) => setCondition(index, { kind })}
              />
              {condition.kind !== 'has-attachment' && (
                <Input
                  className="col-start-1 min-w-0"
                  aria-label={t('mail.rules.form.value')}
                  placeholder={t('mail.rules.form.value')}
                  value={condition.value}
                  onChange={(e) => setCondition(index, { value: e.target.value })}
                />
              )}
              <Button
                type="text"
                className="col-start-2 row-start-1"
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
            <div key={index} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-2 sm:flex">
              <Select
                className="col-start-1 w-full min-w-0 sm:w-52 sm:shrink-0"
                aria-label={t('mail.rules.form.then')}
                value={action.kind}
                options={actionKinds(provider).map((kind) => ({ value: kind, label: t(`mail.rules.action.${kind}`) }))}
                onChange={(kind) => setAction(index, { kind })}
              />
              {needsLabel(action.kind) && (
                <Select
                  className="col-start-1 min-w-0 sm:flex-1"
                  aria-label={t(provider === 'gmail' ? 'mail.rules.form.label' : 'mail.rules.form.folder')}
                  placeholder={t(provider === 'gmail' ? 'mail.rules.form.label' : 'mail.rules.form.folder')}
                  showSearch={{ optionFilterProp: 'label' }}
                  value={action.labelId ?? undefined}
                  options={labelOptions}
                  onChange={(labelId) => setAction(index, { labelId })}
                />
              )}
              <Button
                type="text"
                className="col-start-2 row-start-1"
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

        {error && <Alert type="error" showIcon title={t(`mail.rules.error.${error}`)} />}
      </div>
    </Modal>
  )
}
