import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Form, Input, Modal, Select, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useCreatePageFromTemplate, usePageTree } from '../hooks/usePages'

interface TemplatePageModalProps {
  spaceId: string | null
  folderId: string | null
  onClose: () => void
  onCreated: (id: string) => void
}

/** Chooses an owned template and creates an independent page in the current destination. */
export function TemplatePageModal({ spaceId, folderId, onClose, onCreated }: TemplatePageModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  // Models from every space can be used in the current space or folder.
  const { data: pages = [], isLoading, isError } = usePageTree()
  const templates = pages.filter((page) => page.isTemplate && !page.isArchived)
  const create = useCreatePageFromTemplate()
  const [templateId, setTemplateId] = useState<string>()
  const [title, setTitle] = useState('')

  async function submit() {
    if (!templateId || !title.trim() || create.isPending) return
    try {
      const page = await create.mutateAsync({ templateId, title: title.trim(), spaceId, folderId })
      onCreated(page.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  return (
    <Modal
      open
      title={t('notes.templates.create')}
      onCancel={onClose}
      onOk={() => void submit()}
      okText={t('notes.newPage')}
      confirmLoading={create.isPending}
      okButtonProps={{ disabled: !templateId || !title.trim() || isError }}
    >
      {isError ? <Alert type="error" title={t('notes.loadError')} /> : (
        <Form layout="vertical">
          <Form.Item label={t('notes.templates.choose')}>
            <Select
              aria-label={t('notes.templates.choose')}
              value={templateId}
              loading={isLoading}
              showSearch={{ optionFilterProp: 'label' }}
              notFoundContent={t('notes.templates.empty')}
              options={templates.map((page) => ({ value: page.id, label: page.title }))}
              onChange={(id) => {
                setTemplateId(id)
                setTitle(templates.find((page) => page.id === id)?.title ?? '')
              }}
            />
          </Form.Item>
          <Form.Item label={t('notes.templates.pageTitle')}>
            <Input aria-label={t('notes.templates.pageTitle')} value={title} maxLength={255} onChange={(event) => setTitle(event.target.value)} onPressEnter={() => void submit()} />
          </Form.Item>
          <Typography.Paragraph type="secondary">{t('notes.templates.variables')}</Typography.Paragraph>
        </Form>
      )}
    </Modal>
  )
}
