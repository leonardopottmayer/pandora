import { useTranslation } from 'react-i18next'
import { Alert, Button, Table, Typography, theme, type TableProps } from 'antd'
import { FileTextOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useFolderProperties } from '../hooks/useFolders'
import type { FolderPropertyPageDto } from '../models'
import type { SpaceNode } from '../lib/spaceTree'

interface FolderPropertiesTableProps {
  folderId: string
  includeArchived: boolean
  tagIds: string[]
  onOpen: (node: SpaceNode) => void
}

/** A read-only projection of the direct pages' properties; editing happens in the page itself. */
export function FolderPropertiesTable({ folderId, includeArchived, tagIds, onOpen }: FolderPropertiesTableProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const query = useFolderProperties(folderId, includeArchived, tagIds)
  if (query.isError) return <Alert type="error" showIcon title={toErrorMessage(query.error, t('notes.loadError'))} />

  const names = query.data?.columns ?? []
  const columns: TableProps<FolderPropertyPageDto>['columns'] = [
    {
      key: 'page', title: t('notes.folderTable.page'), width: 260,
      render: (_, { page }) => (
        <Button type="link" className="!h-auto !max-w-full !whitespace-normal !p-0 !text-left" aria-label={page.title || t('notes.untitled')} onClick={() => onOpen({ kind: 'page', id: page.id, page, children: [] })}>
          <span aria-hidden="true">{page.icon || <FileTextOutlined />}</span>
          <span>{page.title || t('notes.untitled')}</span>
        </Button>
      ),
    },
    ...names.map((name) => ({
      key: `property-${name}`, title: name, dataIndex: ['properties', name], width: 200,
      render: (value: string | undefined) => <span style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{value || '—'}</span>,
    })),
  ]

  return (
    <div className="flex flex-col gap-2">
      <Typography.Text type="secondary" className="text-xs">{t('notes.folderTable.readOnly')}</Typography.Text>
      <div className="overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
        <Table<FolderPropertyPageDto>
          rowKey={(row) => row.page.id}
          loading={query.isLoading}
          columns={columns}
          dataSource={query.data?.pages ?? []}
          pagination={false}
          size="middle"
          tableLayout="fixed"
          scroll={{ x: 260 + names.length * 200 }}
          locale={{ emptyText: t('notes.folderTable.empty') }}
        />
      </div>
    </div>
  )
}
