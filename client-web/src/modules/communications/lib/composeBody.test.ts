import { beforeEach, describe, expect, it } from 'vitest'
import { clearDraft, loadDraft, markdownToHtml, newSendKey, quoteForReply, saveDraft } from './composeBody'
import { toSendForm } from '../services/communications.service'

describe('markdownToHtml', () => {
  it('formats markdown and keeps line breaks', () => {
    const html = markdownToHtml('**bold**\nnext')
    expect(html).toContain('<strong>bold</strong>')
    expect(html).toContain('<br')
  })

  it('removes scripts, handlers and styles a quote could carry', () => {
    const html = markdownToHtml('<script>alert(1)</script>\n\n<a href="x" onclick="alert(1)" style="color:red">link</a>')
    expect(html).not.toContain('<script')
    expect(html).not.toContain('onclick')
    expect(html).not.toContain('style=')
  })

  it('removes images, so a quoted tracker cannot fire in the preview', () => {
    expect(markdownToHtml('![x](https://tracker.example/open.gif)')).not.toContain('tracker.example')
    expect(markdownToHtml('<img src="https://tracker.example/open.gif">')).not.toContain('tracker.example')
  })
})

describe('quoteForReply', () => {
  it('quotes every line under the heading, with room to write above', () => {
    expect(quoteForReply('Ana wrote:', 'one\r\ntwo\n')).toBe('\n\nAna wrote:\n> one\n> two')
  })
})

describe('newSendKey', () => {
  it('makes a different uuid each time', () => {
    const key = newSendKey()
    expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
    expect(newSendKey()).not.toBe(key)
  })
})

describe('drafts', () => {
  beforeEach(() => localStorage.clear())

  it('come back until cleared', () => {
    saveDraft('new', { subject: 'Hi', to: ['a@b.test'] })
    expect(loadDraft('new')).toEqual({ subject: 'Hi', to: ['a@b.test'] })
    clearDraft('new')
    expect(loadDraft('new')).toBeNull()
  })

  it('ignore what cannot be read', () => {
    localStorage.setItem('mail.compose.draft.broken', '{not json')
    expect(loadDraft('broken')).toBeNull()
  })
})

describe('toSendForm', () => {
  it('sends one field per address and per kept file', () => {
    const file = new File(['x'], 'a.txt')
    const form = toSendForm({
      mailboxId: 'mb1',
      sendKey: 'key',
      to: ['a@x.test', 'b@x.test'],
      cc: [],
      bcc: ['c@x.test'],
      subject: 'S',
      textBody: 'T',
      htmlBody: '<p>T</p>',
      forwardOfId: 'm1',
      keepAttachmentIds: ['0', '2'],
      files: [file],
    })

    expect(form.getAll('to')).toEqual(['a@x.test', 'b@x.test'])
    expect(form.getAll('cc')).toEqual([])
    expect(form.getAll('bcc')).toEqual(['c@x.test'])
    expect(form.getAll('keepAttachmentIds')).toEqual(['0', '2'])
    expect(form.get('forwardOfId')).toBe('m1')
    expect(form.get('replyToId')).toBeNull()
    expect((form.get('files') as File).name).toBe('a.txt')
  })
})
