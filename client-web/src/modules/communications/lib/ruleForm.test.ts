import { describe, expect, it } from 'vitest'
import type { MailLabel, MailRule } from '../models'
import { draftFrom, newDraft, ruleLabels, takeRuleForm, saveRuleForm, toRuleInput, validateRule, type RuleDraft } from './ruleForm'

const good: RuleDraft = { ...newDraft(), name: 'Rule', conditions: [{ kind: 'from', value: 'ana' }] }

const label = (id: string, kind: 'user' | 'system', role: MailLabel['role'] = null, isExclusive = false): MailLabel => ({
  id,
  name: id,
  kind,
  role,
  isExclusive,
  total: 0,
  unread: 0,
})

describe('validateRule', () => {
  it('passes a good rule', () => {
    expect(validateRule(good, 'titan')).toBeNull()
  })

  it('refuses a rule that would match all mail', () => {
    expect(validateRule({ ...good, conditions: [] }, 'titan')).toBe('noConditions')
    expect(validateRule({ ...good, conditions: [{ kind: 'from', value: '  ' }] }, 'titan')).toBe('emptyValue')
    expect(validateRule({ ...good, conditions: [{ kind: 'has-attachment', value: '' }] }, 'titan')).toBeNull()
  })

  it('needs a name that fits, an action, and a label where one is needed', () => {
    expect(validateRule({ ...good, name: ' ' }, 'titan')).toBe('name')
    expect(validateRule({ ...good, name: 'x'.repeat(101) }, 'titan')).toBe('name')
    expect(validateRule({ ...good, actions: [] }, 'titan')).toBe('noActions')
    expect(validateRule({ ...good, actions: [{ kind: 'move', labelId: null }] }, 'titan')).toBe('noLabel')
  })

  it('holds Gmail to what one filter has fields for', () => {
    const two: RuleDraft = { ...good, conditions: [{ kind: 'from', value: 'a' }, { kind: 'from', value: 'b' }] }
    const words: RuleDraft = { ...good, conditions: [{ kind: 'has-words', value: 'a' }, { kind: 'provider-query', value: 'b' }] }
    const labels: RuleDraft = { ...good, actions: [{ kind: 'add-label', labelId: 'a' }, { kind: 'move', labelId: 'b' }] }

    expect(validateRule(two, 'gmail')).toBe('gmailOneOfEach')
    expect(validateRule(two, 'titan')).toBeNull()
    expect(validateRule(words, 'gmail')).toBe('gmailOneOfEach')
    expect(validateRule(labels, 'gmail')).toBe('gmailOneLabel')
  })
})

describe('ruleLabels', () => {
  it('offers Gmail only user labels, and IMAP the folders a message can move to', () => {
    const labels = [label('work', 'user', null, true), label('important', 'system'), label('sent', 'system', 'sent', true), label('junk', 'system', 'spam', true)]

    expect(ruleLabels(labels, 'gmail').map((l) => l.id)).toEqual(['work'])
    expect(ruleLabels(labels, 'titan').map((l) => l.id)).toEqual(['work', 'junk'])
  })
})

describe('the form', () => {
  it('trims what goes out and leaves no label where none belongs', () => {
    const input = toRuleInput({
      name: ' Rule ',
      conditions: [{ kind: 'from', value: ' ana ' }, { kind: 'has-attachment', value: 'stale' }],
      actions: [{ kind: 'star', labelId: 'stale' }],
    })

    expect(input).toEqual({
      name: 'Rule',
      conditions: [{ kind: 'from', value: 'ana' }, { kind: 'has-attachment', value: '' }],
      actions: [{ kind: 'star', labelId: null }],
    })
  })

  it('empties a label that is gone, so it has to be chosen again', () => {
    const rule = { name: 'R', conditions: [], actions: [{ kind: 'move', labelId: 'gone' }] } as unknown as MailRule

    expect(draftFrom(rule, [label('work', 'user')]).actions[0].labelId).toBeNull()
  })

  it('comes back once after a consent', () => {
    saveRuleForm({ mailboxId: 'mb1', ruleId: null, draft: good })

    expect(takeRuleForm()?.draft.name).toBe('Rule')
    expect(takeRuleForm()).toBeNull()
  })
})
