import type { MailLabel, MailProvider, MailRule, RuleAction, RuleActionKind, RuleCondition, RuleConditionKind } from '../models'
import { canMoveTo } from '../models'
import type { RuleInput } from '../services/communications.service'

export interface RuleDraft {
  name: string
  conditions: RuleCondition[]
  actions: RuleAction[]
}

const NAME_MAX_LENGTH = 100

export const newCondition = (): RuleCondition => ({ kind: 'from', value: '' })
export const newAction = (): RuleAction => ({ kind: 'mark-read', labelId: null })
export const newDraft = (): RuleDraft => ({ name: '', conditions: [newCondition()], actions: [newAction()] })

export function needsLabel(kind: RuleActionKind): boolean {
  return kind === 'add-label' || kind === 'move'
}

export function conditionKinds(provider: MailProvider): RuleConditionKind[] {
  const kinds: RuleConditionKind[] = ['from', 'to', 'subject', 'has-words', 'has-attachment']
  return provider === 'gmail' ? [...kinds, 'provider-query'] : kinds
}

/** "Add" is not something a folder can do: a message lives in one. */
export function actionKinds(provider: MailProvider): RuleActionKind[] {
  const kinds: RuleActionKind[] = ['move', 'archive', 'mark-read', 'star', 'trash']
  return provider === 'gmail' ? ['add-label', ...kinds] : kinds
}

/** The same rule the server applies: on Gmail a filter takes only a user label. */
export function ruleLabels(labels: MailLabel[], provider: MailProvider): MailLabel[] {
  return labels.filter((l) => (provider === 'gmail' ? l.kind === 'user' : canMoveTo(l)))
}

/** A rule as a form. A label that is gone is left empty, so it has to be chosen again. */
export function draftFrom(rule: MailRule, labels: MailLabel[]): RuleDraft {
  return {
    name: rule.name,
    conditions: rule.conditions,
    actions: rule.actions.map((a) => ({ ...a, labelId: labels.some((l) => l.id === a.labelId) ? a.labelId : null })),
  }
}

/** The key of what is wrong under `mail.rules.error`, or null. The same checks as the server, in words for this form. */
export function validateRule(draft: RuleDraft, provider: MailProvider): string | null {
  const name = draft.name.trim()
  if (!name || name.length > NAME_MAX_LENGTH) return 'name'

  if (draft.conditions.length === 0) return 'noConditions'
  if (draft.conditions.some((c) => c.kind !== 'has-attachment' && !c.value.trim())) return 'emptyValue'

  if (draft.actions.length === 0) return 'noActions'
  const labelled = draft.actions.filter((a) => needsLabel(a.kind))
  if (labelled.some((a) => !a.labelId)) return 'noLabel'

  if (provider === 'gmail') {
    const kinds = draft.conditions.map((c) => (c.kind === 'provider-query' ? 'has-words' : c.kind))
    if (new Set(kinds).size < kinds.length) return 'gmailOneOfEach'
    if (new Set(labelled.map((a) => a.labelId)).size > 1) return 'gmailOneLabel'
  }

  return null
}

export function toRuleInput(draft: RuleDraft): RuleInput {
  return {
    name: draft.name.trim(),
    conditions: draft.conditions.map((c) => ({ kind: c.kind, value: c.kind === 'has-attachment' ? '' : c.value.trim() })),
    actions: draft.actions.map((a) => ({ kind: a.kind, labelId: needsLabel(a.kind) ? a.labelId : null })),
  }
}

/** The form a consent should not lose: Google's page replaces this one. */
export interface SavedRuleForm {
  mailboxId: string
  ruleId: string | null
  draft: RuleDraft
}

const SAVED_FORM_KEY = 'mail.rules.form'

export function saveRuleForm(form: SavedRuleForm): void {
  try {
    sessionStorage.setItem(SAVED_FORM_KEY, JSON.stringify(form))
  } catch {
    // Without storage, the form is only lost.
  }
}

/** Once: a refresh after coming back does not reopen it. */
export function takeRuleForm(): SavedRuleForm | null {
  try {
    const saved = sessionStorage.getItem(SAVED_FORM_KEY)
    sessionStorage.removeItem(SAVED_FORM_KEY)
    return saved ? (JSON.parse(saved) as SavedRuleForm) : null
  } catch {
    return null
  }
}
