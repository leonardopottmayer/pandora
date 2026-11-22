import { useState } from 'react'
import { App, Button, Popconfirm, Space, Spin, Switch, Table, Tag, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import * as devicesService from '@/modules/identity/services/devices.service'
import { FilesGate } from '../components/FilesGate'
import { FilterFormModal } from '../components/FilterFormModal'
import { filesKeys, useFilters, useRoots } from '../hooks/useFiles'
import * as filesService from '../services/files.service'
import type { Filter } from '../models'

/** Name rules on top of the selection: what is dropped (or the only things kept), at any scope. */
export function FiltersPage() {
  return (
    <FilesGate>
      <Filters />
    </FilesGate>
  )
}

function Filters() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const filters = useFilters()
  const roots = useRoots()
  const devices = useQuery({ queryKey: ['identity', 'devices'], queryFn: devicesService.listDevices })
  const [editing, setEditing] = useState<Filter | 'new' | null>(null)

  const save = useMutation({
    mutationFn: (f: Filter) => filesService.saveFilter(f.id, f),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: filesKeys.filters() }),
    onError: (e) => message.error(toErrorMessage(e, t('files.filters.saveError'))),
  })
  const remove = useMutation({
    mutationFn: filesService.deleteFilter,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: filesKeys.filters() }),
    onError: (e) => message.error(toErrorMessage(e, t('files.filters.deleteError'))),
  })

  if (filters.isLoading) return <Spin className="block py-16 text-center" />

  function scopeOf(f: Filter): string {
    if (f.rootId) {
      const name = roots.data?.find((r) => r.id === f.rootId)?.name ?? '?'
      return f.scopePath ? `${name} ${f.scopePath}` : name
    }
    if (f.deviceId) return devices.data?.find((d) => d.id === f.deviceId)?.name ?? '?'
    return t('files.filters.scopeAccount')
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeading title={t('files.filters.title')} description={t('files.filters.intro')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>
          {t('files.filters.add')}
        </Button>
      </div>

      <Table<Filter>
        rowKey="id"
        size="small"
        pagination={false}
        scroll={{ x: true }}
        dataSource={filters.data ?? []}
        columns={[
          {
            title: t('files.filters.name'),
            render: (_, f) => (
              <span>
                {f.name} {f.isBuiltin && <Tag>{t('files.filters.builtin')}</Tag>}
              </span>
            ),
          },
          {
            title: t('files.filters.rule'),
            render: (_, f) => (
              <Space size={4} wrap>
                <Tag color={f.action === 'include' ? 'green' : 'red'}>{t(`files.filters.actionName.${f.action}`)}</Tag>
                <span>{t(`files.filters.target.${f.appliesTo}`)}</span>
                <Typography.Text type="secondary">{t(`files.filters.matcherName.${f.matcher}`)}</Typography.Text>
                <Typography.Text code>{f.pattern}</Typography.Text>
              </Space>
            ),
          },
          { title: t('files.filters.scope'), render: (_, f) => scopeOf(f) },
          {
            title: t('files.filters.enabled'),
            render: (_, f) => (
              <Switch size="small" checked={f.isEnabled} onChange={(isEnabled) => save.mutate({ ...f, isEnabled })} />
            ),
          },
          {
            title: t('common.actions'),
            render: (_, f) => (
              <Space>
                <Button size="small" onClick={() => setEditing(f)}>
                  {t('common.edit')}
                </Button>
                <Popconfirm
                  title={t('files.filters.deleteConfirm', { name: f.name })}
                  okButtonProps={{ danger: true }}
                  onConfirm={() => remove.mutate(f.id)}
                >
                  <Button size="small" danger>
                    {t('common.delete')}
                  </Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />

      <Typography.Text type="secondary">{t('files.selection.nextScan')}</Typography.Text>

      {editing && (
        <FilterFormModal
          filter={editing === 'new' ? null : editing}
          devices={devices.data ?? []}
          roots={roots.data ?? []}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  )
}
