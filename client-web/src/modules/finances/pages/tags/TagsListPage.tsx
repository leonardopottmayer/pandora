import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Flex, Popconfirm, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TagDto } from '../../models'
import { useDeleteTag, useTags } from '../../hooks/useTags'
import { TagFormModal } from './TagFormModal'

export function TagsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TagDto | null>(null)

  const { data, isLoading } = useTags()
  const deleteMutation = useDeleteTag()

  function openCreate() {
    setEditing(null)
    setModalOpen(true)
  }

  async function handleDelete(tag: TagDto) {
    try {
      await deleteMutation.mutateAsync(tag.id)
      message.success(t('finances.tags.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.tags.deleteError')))
    }
  }

  const columns: ColumnsType<TagDto> = [
    {
      title: t('finances.tags.name'),
      dataIndex: 'name',
      render: (name: string, tag) => <Tag color={tag.color ?? undefined}>{name}</Tag>,
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, tag) => (
        <Space>
          <Button
            size="small"
            onClick={() => {
              setEditing(tag)
              setModalOpen(true)
            }}
          >
            {t('common.edit')}
          </Button>
          <Popconfirm
            title={t('finances.tags.deleteConfirm')}
            okText={t('common.delete')}
            cancelText={t('common.cancel')}
            onConfirm={() => handleDelete(tag)}
          >
            <Button size="small" danger>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Card>
      <Flex justify="space-between" align="center" className="mb-4">
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('nav.tags')}
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('finances.tags.new')}
        </Button>
      </Flex>

      <Table rowKey="id" loading={isLoading} dataSource={data} columns={columns} pagination={false} />

      <TagFormModal open={modalOpen} tag={editing} onClose={() => setModalOpen(false)} />
    </Card>
  )
}
