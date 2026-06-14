import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Flex, Space, Switch, Table, Tree, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { DataNode } from 'antd/es/tree'
import { PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { SystemCategoryDto, UserCategoryDto } from '../../models'
import { TRANSACTION_NATURE_META } from '../../lib/enums'
import { EnumTag } from '../../components/EnumTag'
import {
  useSetUserCategoryActive,
  useSystemCategories,
  useUserCategories,
} from '../../hooks/useCategories'
import { CategoryFormModal } from './CategoryFormModal'

function systemTree(cats: SystemCategoryDto[]): DataNode[] {
  return cats.map((c) => ({
    key: c.id,
    title: c.name,
    children: c.children?.length ? systemTree(c.children) : undefined,
  }))
}

export function CategoriesListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [includeInactive, setIncludeInactive] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<UserCategoryDto | null>(null)

  const { data: userCategories, isLoading } = useUserCategories(includeInactive)
  const { data: systemCategories } = useSystemCategories()
  const activeMutation = useSetUserCategoryActive()

  async function toggleActive(category: UserCategoryDto) {
    const active = !category.isActive
    try {
      await activeMutation.mutateAsync({ id: category.id, active })
      message.success(active ? t('finances.categories.activated') : t('finances.categories.deactivated'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.categories.saveError')))
    }
  }

  const columns: ColumnsType<UserCategoryDto> = [
    { title: t('finances.categories.name'), dataIndex: 'name' },
    {
      title: t('finances.categories.nature'),
      dataIndex: 'nature',
      render: (_, c) => <EnumTag meta={TRANSACTION_NATURE_META[c.nature]} />,
    },
    {
      title: t('finances.categories.active'),
      dataIndex: 'isActive',
      render: (isActive: boolean, c) => (
        <Switch checked={isActive} onChange={() => toggleActive(c)} size="small" />
      ),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, c) => (
        <Button
          size="small"
          onClick={() => {
            setEditing(c)
            setModalOpen(true)
          }}
        >
          {t('common.edit')}
        </Button>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('finances.categories.userTitle')}
          </Typography.Title>
          <Space>
            <Switch checked={includeInactive} onChange={setIncludeInactive} size="small" />
            <span>{t('finances.categories.showInactive')}</span>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                setEditing(null)
                setModalOpen(true)
              }}
            >
              {t('finances.categories.new')}
            </Button>
          </Space>
        </Flex>

        <Table
          rowKey="id"
          loading={isLoading}
          dataSource={userCategories}
          columns={columns}
          pagination={false}
          expandable={{ childrenColumnName: 'children' }}
        />
      </Card>

      <Card title={t('finances.categories.systemTitle')}>
        <Typography.Paragraph type="secondary">
          {t('finances.categories.systemDesc')}
        </Typography.Paragraph>
        <Tree treeData={systemTree(systemCategories ?? [])} selectable={false} />
      </Card>

      <CategoryFormModal open={modalOpen} category={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
