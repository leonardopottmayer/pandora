import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Form, Modal, Select, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useNotesPreferences, useSaveNotesPreferences } from '../hooks/useDailyNotes'
import { usePageTree } from '../hooks/usePages'

/** Selects the template for future daily notes, leaving existing entries untouched. */
export function DailyNotesSettingsModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const preferences = useNotesPreferences()
  const pages = usePageTree()
  const save = useSaveNotesPreferences()
  const [selected, setSelected] = useState<string>()
  const value = selected ?? preferences.data?.dailyTemplateId ?? ''
  const templates = (pages.data ?? []).filter((page) => page.isTemplate && !page.isArchived)
  const unavailable = !!value && !templates.some((page) => page.id === value)
  const loading = preferences.isLoading || pages.isLoading
  const failed = preferences.isError || pages.isError

  async function submit() {
    if (loading || failed || save.isPending) return
    try {
      await save.mutateAsync({ dailyTemplateId: value || null })
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  return (
    <Modal open title={t('notes.daily.settings')} onCancel={onClose} onOk={() => void submit()} okText={t('common.save')} confirmLoading={save.isPending} okButtonProps={{ disabled: loading || failed || unavailable }}>
      {failed ? <Alert type="error" title={t('notes.loadError')} /> : (
        <Form layout="vertical">
          <Form.Item label={t('notes.daily.template')}>
            <Select
              aria-label={t('notes.daily.template')}
              loading={loading}
              value={value}
              onChange={setSelected}
              showSearch={{ optionFilterProp: 'label' }}
              options={[
                { value: '', label: t('notes.daily.noTemplate') },
                ...templates.map((page) => ({ value: page.id, label: page.title })),
                ...(unavailable ? [{ value, label: t('notes.daily.unavailable') }] : []),
              ]}
            />
          </Form.Item>
          <Typography.Paragraph type="secondary">{t('notes.daily.templateHint')}</Typography.Paragraph>
        </Form>
      )}
    </Modal>
  )
}
