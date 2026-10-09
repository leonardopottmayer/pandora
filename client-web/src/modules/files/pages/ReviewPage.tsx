import type { ReactNode } from 'react'
import { App, Badge, Button, Empty, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { FileOutlined, FolderOutlined, HddOutlined } from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import { FilesGate } from '../components/FilesGate'
import { LazyLevel, TreeRow } from '../components/LazyTree'
import { filesKeys } from '../hooks/useFiles'
import * as filesService from '../services/files.service'
import type { ReviewNode } from '../models'

/**
 * Missing and excluded entries waiting for a decision, as each root's folder tree pruned to what waits
 * (product-plan §4.7). A decision on a folder covers everything waiting at or below it.
 */
export function ReviewPage() {
  return (
    <FilesGate>
      <Review />
    </FilesGate>
  )
}

function Review() {
  const { t } = useTranslation()
  const roots = useQuery({ queryKey: [...filesKeys.review(), 'roots'], queryFn: () => filesService.reviewTree() })

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6">
      <PageHeading title={t('files.review.title')} description={t('files.review.intro')} />
      {roots.isLoading ? (
        <Spin className="block py-16 text-center" />
      ) : roots.data?.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('files.review.empty')} />
      ) : (
        <SettingsSection bodyClassName="p-2">
          {roots.data?.map((root) => (
            <TreeRow
              key={root.rootId}
              depth={0}
              expandable
              label={<NodeLabel node={root} icon={<HddOutlined />} />}
              renderChildren={() => <Level rootId={root.rootId} parent="/" depth={1} />}
            />
          ))}
        </SettingsSection>
      )}
    </div>
  )
}

function Level({ rootId, parent, depth }: { rootId: string; parent: string; depth: number }) {
  return (
    <LazyLevel
      queryKey={[...filesKeys.review(), rootId, parent]}
      load={() => filesService.reviewTree(rootId, parent)}
      depth={depth}
    >
      {(node: ReviewNode) => (
        <TreeRow
          key={node.path}
          depth={depth}
          expandable={node.hasChildren}
          label={<NodeLabel node={node} icon={node.kind === 'file' ? <FileOutlined /> : <FolderOutlined />} />}
          renderChildren={() => <Level rootId={rootId} parent={node.path} depth={depth + 1} />}
        />
      )}
    </LazyLevel>
  )
}

function NodeLabel({ node, icon }: { node: ReviewNode; icon: ReactNode }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()

  const decide = useMutation({
    mutationFn: (decision: 'forget' | 'keep') =>
      filesService.decideReview(decision, { entryIds: [], folders: [{ rootId: node.rootId, path: node.path }] }),
    onSuccess: (count, decision) => {
      // Counts change all the way up, and the catalog loses forgotten entries.
      void queryClient.invalidateQueries({ queryKey: filesKeys.all })
      message.success(t(`files.review.${decision}Done`, { count }))
    },
    onError: (e) => message.error(toErrorMessage(e, t('files.review.error'))),
  })

  return (
    <div className="flex min-w-0 flex-1 flex-wrap items-center justify-between gap-2 py-0.5">
      <Space size={6} className="min-w-0">
        {icon}
        <Typography.Text ellipsis>{node.name}</Typography.Text>
        {node.status && <Tag color={node.status === 'missing' ? 'orange' : 'default'}>{t(`files.status.${node.status}`)}</Tag>}
        {(node.hasChildren || node.count > 1) && <Badge count={node.count} overflowCount={99999} color="gray" />}
      </Space>
      <Space size={4}>
        <Button size="small" loading={decide.isPending && decide.variables === 'keep'} onClick={() => decide.mutate('keep')}>
          {t('files.review.keep')}
        </Button>
        <Popconfirm
          title={t('files.review.forgetConfirm', { count: node.count })}
          description={t('files.review.forgetDesc')}
          okButtonProps={{ danger: true }}
          onConfirm={() => decide.mutate('forget')}
        >
          <Button size="small" danger loading={decide.isPending && decide.variables === 'forget'}>
            {t('files.review.forget')}
          </Button>
        </Popconfirm>
      </Space>
    </div>
  )
}
