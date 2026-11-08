import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Form, Input, Modal, Select, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useSpaces } from '@/modules/notes/hooks/useSpaces'
import { useFolders } from '@/modules/notes/hooks/useFolders'
import { noteKeys } from '@/modules/notes/hooks/queryKeys'
import { buildSpaceTree, pathTo } from '@/modules/notes/lib/spaceTree'
import { saveMessageToNotes } from '../services/communications.service'

/** Copies one message into the chosen Notes destination, then opens the independent page. */
export function SaveMessageToNotesModal({ messageId, subject, onClose }: { messageId: string; subject: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const client = useQueryClient()
  const spaces = useSpaces()
  const folders = useFolders()
  const [title, setTitle] = useState(() => (subject || t('mail.noSubject')).slice(0, 255))
  const [selectedSpace, setSelectedSpace] = useState<string>()
  const [folderId, setFolderId] = useState('')
  const spaceId = selectedSpace ?? spaces.data?.find((space) => space.isDefault)?.id ?? spaces.data?.[0]?.id ?? ''
  const availableFolders = (folders.data ?? []).filter((folder) => folder.spaceId === spaceId)
  const tree = buildSpaceTree(availableFolders, [])
  const failed = spaces.isError || folders.isError
  const loading = spaces.isLoading || folders.isLoading
  const save = useMutation({
    mutationFn: () => saveMessageToNotes(messageId, { title: title.trim(), spaceId: spaceId || null, folderId: folderId || null }),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: noteKeys.pages() })
      client.invalidateQueries({ queryKey: noteKeys.spaces() })
      client.invalidateQueries({ queryKey: noteKeys.tags() })
    },
  })

  async function submit() {
    if (!title.trim() || failed || loading || save.isPending) return
    try {
      const id = await save.mutateAsync()
      onClose()
      navigate(`/notes/${id}`)
    } catch (err) {
      message.error(toErrorMessage(err, t('mail.saveToNotes.error')))
    }
  }

  return (
    <Modal open title={t('mail.saveToNotes.title')} onCancel={onClose} onOk={() => void submit()} okText={t('common.save')} confirmLoading={save.isPending}
      okButtonProps={{ disabled: !title.trim() || failed || loading }} cancelButtonProps={{ disabled: save.isPending }} closable={!save.isPending} mask={{ closable: !save.isPending }} keyboard={!save.isPending}>
      {failed ? <Alert type="error" title={t('mail.saveToNotes.loadError')} /> : (
        <Form layout="vertical">
          <Form.Item label={t('mail.saveToNotes.pageTitle')}>
            <Input aria-label={t('mail.saveToNotes.pageTitle')} value={title} maxLength={255} onChange={(event) => setTitle(event.target.value)} onPressEnter={() => void submit()} />
          </Form.Item>
          <Form.Item label={t('mail.saveToNotes.space')}>
            <Select aria-label={t('mail.saveToNotes.space')} value={spaceId} loading={spaces.isLoading} options={spaces.data?.length
              ? spaces.data.map((space) => ({ value: space.id, label: space.name })) : [{ value: '', label: t('mail.saveToNotes.defaultSpace') }]}
              onChange={(id) => { setSelectedSpace(id); setFolderId('') }} />
          </Form.Item>
          <Form.Item label={t('mail.saveToNotes.folder')}>
            <Select aria-label={t('mail.saveToNotes.folder')} value={folderId} loading={folders.isLoading} showSearch={{ optionFilterProp: 'label' }}
              options={[{ value: '', label: t('mail.saveToNotes.root') }, ...availableFolders.map((folder) => ({ value: folder.id,
                label: [...pathTo(tree, folder.id).map((node) => node.kind === 'folder' ? node.folder.name : ''), folder.name].join(' / '),
              }))]} onChange={setFolderId} />
          </Form.Item>
          <Typography.Paragraph type="secondary">{t('mail.saveToNotes.hint')}</Typography.Paragraph>
        </Form>
      )}
    </Modal>
  )
}
