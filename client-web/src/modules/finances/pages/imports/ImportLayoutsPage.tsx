import { useTranslation } from 'react-i18next'
import { Card, Table, Tag } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { ImportLayoutDto } from '../../models'
import { useImportLayouts } from '../../hooks/useImports'

export function ImportLayoutsPage() {
  const { t } = useTranslation()
  const { data, isLoading } = useImportLayouts()

  const columns: ColumnsType<ImportLayoutDto> = [
    {
      title: t('finances.importLayouts.name'),
      dataIndex: 'name',
    },
    {
      title: t('finances.importLayouts.bank'),
      dataIndex: 'bankName',
      render: (v) => v ?? '—',
    },
    {
      title: t('finances.importLayouts.format'),
      dataIndex: 'fileFormat',
      render: (v) => <Tag>{v.toUpperCase()}</Tag>,
    },
    {
      title: t('finances.importLayouts.accountType'),
      dataIndex: 'accountType',
      render: (v) => <Tag color={v === 'card' ? 'purple' : 'blue'}>{v}</Tag>,
    },
    {
      title: t('finances.importLayouts.code'),
      dataIndex: 'layoutCode',
      render: (v) => <code style={{ fontSize: 12 }}>{v}</code>,
    },
  ]

  return (
    <Card title={t('finances.importLayouts.title')}>
      <Table<ImportLayoutDto>
        rowKey="id"
        columns={columns}
        dataSource={data}
        loading={isLoading}
        pagination={false}
        size="small"
      />
    </Card>
  )
}
