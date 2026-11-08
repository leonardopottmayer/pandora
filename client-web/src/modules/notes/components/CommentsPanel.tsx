import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Drawer, Empty, Input, Modal, Segmented, Tag, Typography } from 'antd'
import { CheckOutlined, DeleteOutlined, EditOutlined, UndoOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CommentDto } from '../models'
import * as commentsService from '../services/comments.service'
import { useCommentChange } from '../hooks/useComments'
import { timeAgo } from '../lib/timeAgo'

interface CommentsPanelProps {
  pageId: string
  open: boolean
  comments: CommentDto[]
  /** The thread to bring into view, after a click on its words. */
  focusThread: string | null
  /** False for a block no longer in the page. */
  blockExists: (blockId: string) => boolean
  onJump: (blockId: string) => void
  onClose: () => void
}

/** A page's comment threads, open or resolved: read, answered, resolved, edited, deleted. */
export function CommentsPanel({ pageId, open, comments, focusThread, blockExists, onJump, onClose }: CommentsPanelProps) {
  const { t } = useTranslation()
  // The list picked by hand, for as long as the same thread is in focus.
  const [choice, setChoice] = useState<{ focus: string | null; value: 'open' | 'resolved' } | null>(null)
  const threads = comments.filter((c) => !c.threadId)
  const focused = threads.find((c) => c.id === focusThread)

  // A clicked thread shows in its own list, whichever that is, until another list is picked.
  const shown = choice?.focus === focusThread ? choice.value : focused?.resolvedAt ? 'resolved' : 'open'
  useEffect(() => {
    if (focusThread) document.getElementById(`thread-${focusThread}`)?.scrollIntoView?.({ block: 'nearest' })
  }, [focusThread, open])

  const list = threads.filter((c) => (shown === 'resolved') === !!c.resolvedAt)
  return (
    <Drawer title={t('notes.comments.title')} open={open} onClose={onClose} size={400} mask={false}>
      <div className="flex flex-col gap-3">
        <Segmented<'open' | 'resolved'>
          block
          value={shown}
          onChange={(value) => setChoice({ focus: focusThread, value })}
          options={[
            { value: 'open', label: t('notes.comments.open', { count: threads.filter((c) => !c.resolvedAt).length }) },
            { value: 'resolved', label: t('notes.comments.resolved', { count: threads.filter((c) => c.resolvedAt).length }) },
          ]}
        />
        {!list.length ? (
          <Empty description={t(shown === 'open' ? 'notes.comments.noneOpen' : 'notes.comments.noneResolved')} />
        ) : (
          list.map((thread) => (
            <Thread
              key={thread.id}
              pageId={pageId}
              thread={thread}
              replies={comments.filter((c) => c.threadId === thread.id)}
              focused={thread.id === focusThread}
              blockExists={blockExists(thread.blockId)}
              onJump={() => onJump(thread.blockId)}
            />
          ))
        )}
      </div>
    </Drawer>
  )
}

interface ThreadProps {
  pageId: string
  thread: CommentDto
  replies: CommentDto[]
  focused: boolean
  blockExists: boolean
  onJump: () => void
}

function Thread({ pageId, thread, replies, focused, blockExists, onJump }: ThreadProps) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const change = useCommentChange(pageId)
  const [reply, setReply] = useState('')
  const [editing, setEditing] = useState<{ id: string; body: string } | null>(null)

  async function run(action: () => Promise<unknown>): Promise<boolean> {
    try {
      await change.mutateAsync(action)
      return true
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
      return false
    }
  }

  function remove(comment: CommentDto) {
    modal.confirm({
      title: t(comment.threadId ? 'notes.comments.deleteReply' : 'notes.comments.deleteThread'),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => run(() => commentsService.deleteComment(comment.id)),
    })
  }

  const entry = (comment: CommentDto) => (
    <li key={comment.id} className="group flex flex-col gap-1">
      {editing?.id === comment.id ? (
        <div className="flex flex-col gap-1">
          <Input.TextArea
            autoFocus
            autoSize={{ minRows: 2, maxRows: 8 }}
            maxLength={5000}
            value={editing.body}
            aria-label={t('notes.comments.edit')}
            onChange={(event) => setEditing({ id: comment.id, body: event.target.value })}
          />
          <div className="flex justify-end gap-2">
            <Button size="small" onClick={() => setEditing(null)}>{t('common.cancel')}</Button>
            <Button
              size="small"
              type="primary"
              disabled={!editing.body.trim()}
              loading={change.isPending}
              onClick={async () => (await run(() => commentsService.editComment(comment.id, editing.body))) && setEditing(null)}
            >
              {t('common.save')}
            </Button>
          </div>
        </div>
      ) : (
        <Typography.Paragraph className="!mb-0 whitespace-pre-wrap">{comment.body}</Typography.Paragraph>
      )}
      <div className="flex items-center gap-1">
        <Typography.Text type="secondary" className="text-xs">
          {timeAgo(comment.createdAt, i18n.language)}
          {comment.editedAt ? ` · ${t('notes.comments.edited')}` : ''}
        </Typography.Text>
        <span className="ms-auto flex opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
          <Button size="small" type="text" icon={<EditOutlined />} aria-label={t('notes.comments.edit')} onClick={() => setEditing({ id: comment.id, body: comment.body })} />
          <Button size="small" type="text" danger icon={<DeleteOutlined />} aria-label={t('common.delete')} onClick={() => remove(comment)} />
        </span>
      </div>
    </li>
  )

  return (
    <section
      id={`thread-${thread.id}`}
      className="flex flex-col gap-2 rounded-lg p-3"
      style={{ border: `1px solid ${focused ? 'var(--ant-color-primary)' : 'var(--ant-color-border-secondary)'}` }}
    >
      <div className="flex items-start gap-2">
        <button type="button" className="notes-comment-quote min-w-0 flex-1 text-start" onClick={onJump} disabled={!blockExists}>
          {thread.quote ? <q>{thread.quote}</q> : <span>{t('notes.comments.onBlock')}</span>}
        </button>
        {!blockExists && <Tag className="!me-0">{t('notes.comments.blockGone')}</Tag>}
        <Button
          size="small"
          type="text"
          icon={thread.resolvedAt ? <UndoOutlined /> : <CheckOutlined />}
          title={t(thread.resolvedAt ? 'notes.comments.reopen' : 'notes.comments.resolve')}
          aria-label={t(thread.resolvedAt ? 'notes.comments.reopen' : 'notes.comments.resolve')}
          onClick={() => void run(() => commentsService.setCommentResolved(thread.id, !thread.resolvedAt))}
        />
      </div>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {entry(thread)}
        {replies.map(entry)}
      </ul>
      {!thread.resolvedAt && (
        <div className="flex flex-col gap-1">
          <Input.TextArea
            autoSize={{ minRows: 1, maxRows: 6 }}
            maxLength={5000}
            value={reply}
            placeholder={t('notes.comments.replyPlaceholder')}
            aria-label={t('notes.comments.reply')}
            onChange={(event) => setReply(event.target.value)}
          />
          {reply.trim() && (
            <Button
              size="small"
              type="primary"
              className="self-end"
              loading={change.isPending}
              onClick={async () => (await run(() => commentsService.replyToComment(thread.id, reply))) && setReply('')}
            >
              {t('notes.comments.reply')}
            </Button>
          )}
        </div>
      )}
    </section>
  )
}

interface CommentComposerProps {
  /** The words the thread will be about; null for the whole block. */
  quote: string | null
  saving: boolean
  onSubmit: (body: string) => void
  onCancel: () => void
}

/** The first comment of a new thread. */
export function CommentComposer({ quote, saving, onSubmit, onCancel }: CommentComposerProps) {
  const { t } = useTranslation()
  const [body, setBody] = useState('')
  return (
    <Modal
      open
      title={t('notes.comments.new')}
      okText={t('notes.comments.comment')}
      okButtonProps={{ disabled: !body.trim(), loading: saving }}
      cancelText={t('common.cancel')}
      onOk={() => onSubmit(body)}
      onCancel={onCancel}
      destroyOnHidden
    >
      <div className="flex flex-col gap-3">
        {quote && <q className="notes-comment-quote">{quote}</q>}
        <Input.TextArea
          autoFocus
          autoSize={{ minRows: 3, maxRows: 10 }}
          maxLength={5000}
          value={body}
          aria-label={t('notes.comments.new')}
          onChange={(event) => setBody(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && (event.ctrlKey || event.metaKey) && body.trim()) onSubmit(body)
          }}
        />
      </div>
    </Modal>
  )
}
