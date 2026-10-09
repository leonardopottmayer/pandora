import { useState } from 'react'
import { Alert, App, Button, Form, Input, Modal, Select, Switch, Typography } from 'antd'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toErrorMessage } from '@/lib/api/envelope'
import type { Device } from '@/modules/identity/models'
import { filesKeys } from '../hooks/useFiles'
import * as filesService from '../services/files.service'
import type { Filter, FilterInput, FilterPreview, Root } from '../models'

const MATCHERS = ['extension', 'glob', 'starts-with', 'ends-with', 'contains', 'regex'] as const

interface FormValues {
  name: string
  action: FilterInput['action']
  appliesTo: FilterInput['appliesTo']
  matcher: FilterInput['matcher']
  pattern: string
  /** `account`, `device:<id>` or `root:<id>`. */
  scope: string
  scopePath: string
  caseSensitive: 'default' | 'yes' | 'no'
  isEnabled: boolean
}

function toInput(v: FormValues): FilterInput {
  const [kind, id] = v.scope.split(':')
  return {
    name: v.name,
    action: v.appliesTo === 'folder' ? 'exclude' : v.action,
    appliesTo: v.appliesTo,
    matcher: v.matcher,
    pattern: v.pattern,
    deviceId: kind === 'device' ? id : null,
    rootId: kind === 'root' ? id : null,
    scopePath: kind === 'root' && v.scopePath?.trim() ? v.scopePath.trim() : null,
    caseSensitive: v.caseSensitive === 'default' ? null : v.caseSensitive === 'yes',
    isEnabled: v.isEnabled,
  }
}

function toValues(f: Filter | null): FormValues {
  if (!f)
    return { name: '', action: 'exclude', appliesTo: 'file', matcher: 'extension', pattern: '', scope: 'account', scopePath: '', caseSensitive: 'default', isEnabled: true }
  return {
    name: f.name,
    action: f.action,
    appliesTo: f.appliesTo,
    matcher: f.matcher,
    pattern: f.pattern,
    scope: f.rootId ? `root:${f.rootId}` : f.deviceId ? `device:${f.deviceId}` : 'account',
    scopePath: f.scopePath ?? '',
    caseSensitive: f.caseSensitive === null ? 'default' : f.caseSensitive ? 'yes' : 'no',
    isEnabled: f.isEnabled,
  }
}

/** Adds or edits a filter, with a preview of what it matches in the catalog now (product-plan §4.3). */
export function FilterFormModal({
  filter,
  devices,
  roots,
  onClose,
}: {
  filter: Filter | null
  devices: Device[]
  roots: Root[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<FormValues>()
  const appliesTo = Form.useWatch('appliesTo', form)
  const scope = Form.useWatch('scope', form)
  const action = Form.useWatch('action', form)
  const [preview, setPreview] = useState<FilterPreview | null>(null)

  const save = useMutation({
    mutationFn: (v: FormValues) => filesService.saveFilter(filter?.id ?? null, toInput(v)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: filesKeys.filters() })
      message.success(t('files.filters.saved'))
      onClose()
    },
    onError: (e) => message.error(toErrorMessage(e, t('files.filters.saveError'))),
  })

  const runPreview = useMutation({
    mutationFn: async () => filesService.previewFilter(toInput(await form.validateFields())),
    onSuccess: setPreview,
    onError: (e) => {
      setPreview(null)
      // A form left incomplete is already flagged on its fields.
      if (!('errorFields' in (e as object))) message.error(toErrorMessage(e, t('files.filters.previewError')))
    },
  })

  const rootName = (id: string) => roots.find((r) => r.id === id)?.name ?? id

  return (
    <Modal
      open
      title={filter ? t('files.filters.editTitle') : t('files.filters.addTitle')}
      okText={t('common.save')}
      onOk={() => form.submit()}
      confirmLoading={save.isPending}
      onCancel={onClose}
      destroyOnHidden
      width={640}
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={toValues(filter)}
        onFinish={(v) => save.mutate(v)}
        onValuesChange={() => setPreview(null)}
      >
        <Form.Item name="name" label={t('files.filters.name')} rules={[{ required: true, whitespace: true }]}>
          <Input />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-4 sm:grid-cols-2">
          <Form.Item name="appliesTo" label={t('files.filters.appliesTo')} extra={appliesTo === 'folder' ? t('files.filters.folderOnlyExcludes') : null}>
            <Select
              options={[
                { value: 'file', label: t('files.filters.target.file') },
                { value: 'folder', label: t('files.filters.target.folder') },
              ]}
            />
          </Form.Item>
          <Form.Item name="action" label={t('files.filters.action')} extra={t(`files.filters.actionDesc.${appliesTo === 'folder' ? 'exclude' : (action ?? 'exclude')}`)}>
            <Select
              disabled={appliesTo === 'folder'}
              options={[
                { value: 'exclude', label: t('files.filters.actionName.exclude') },
                { value: 'include', label: t('files.filters.actionName.include') },
              ]}
            />
          </Form.Item>
          <Form.Item name="matcher" label={t('files.filters.matcher')}>
            <Select options={MATCHERS.map((m) => ({ value: m, label: t(`files.filters.matcherName.${m}`) }))} />
          </Form.Item>
          <Form.Item name="pattern" label={t('files.filters.pattern')} rules={[{ required: true }]} extra={t('files.filters.patternHint')}>
            <Input />
          </Form.Item>
          <Form.Item name="scope" label={t('files.filters.scope')}>
            <Select
              options={[
                { value: 'account', label: t('files.filters.scopeAccount') },
                ...devices.map((d) => ({ value: `device:${d.id}`, label: t('files.filters.scopeDevice', { name: d.name }) })),
                ...roots.map((r) => ({ value: `root:${r.id}`, label: t('files.filters.scopeRoot', { name: r.name }) })),
              ]}
            />
          </Form.Item>
          {scope?.startsWith('root:') && (
            <Form.Item name="scopePath" label={t('files.filters.scopePath')} extra={t('files.filters.scopePathDesc')}>
              <Input placeholder="/Movies" />
            </Form.Item>
          )}
          <Form.Item name="caseSensitive" label={t('files.filters.caseSensitive')}>
            <Select
              options={[
                { value: 'default', label: t('files.filters.caseDefault') },
                { value: 'no', label: t('files.roots.caseNo') },
                { value: 'yes', label: t('files.roots.caseYes') },
              ]}
            />
          </Form.Item>
          <Form.Item name="isEnabled" label={t('files.filters.enabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
        </div>
      </Form>

      <div className="flex flex-col gap-2">
        <div>
          <Button loading={runPreview.isPending} onClick={() => runPreview.mutate()}>
            {t('files.filters.preview')}
          </Button>
        </div>
        {preview && (
          <Alert
            type="info"
            title={t('files.filters.previewCount', { count: preview.count })}
            description={
              preview.sample.length > 0 && (
                <ul className="m-0 list-none p-0">
                  {preview.sample.map((item) => (
                    <li key={item.id}>
                      <Typography.Text type="secondary">{rootName(item.rootId)}</Typography.Text>{' '}
                      <Typography.Text code>{item.relativePath}</Typography.Text>
                    </li>
                  ))}
                </ul>
              )
            }
          />
        )}
      </div>
    </Modal>
  )
}
