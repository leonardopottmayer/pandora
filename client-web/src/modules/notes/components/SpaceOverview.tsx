import { useTranslation } from 'react-i18next'
import { Button, Empty, Tag, Typography, theme } from 'antd'
import { FileTextOutlined, PlusOutlined } from '@ant-design/icons'
import { usePageTree } from '../hooks/usePages'
import type { SpaceDto } from '../models'
import { SpaceIcon } from './SpaceIcon'

interface SpaceOverviewProps {
  space: SpaceDto
  onOpenPage: (id: string) => void
  onCreatePage: () => void
}

/** What a space shows before a page is picked: what it is, and its top-level pages. */
export function SpaceOverview({ space, onOpenPage, onCreatePage }: SpaceOverviewProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { tree, isLoading } = usePageTree(false, [], space.id)

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6 overflow-auto p-6">
      <div className="flex items-start gap-4">
        <SpaceIcon space={space} size={56} />
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <div className="flex items-center gap-2">
            <Typography.Title level={3} className="!mb-0 truncate">
              {space.name}
            </Typography.Title>
            {space.isDefault && <Tag>{t('notes.spaces.default')}</Tag>}
          </div>
          {space.description && <Typography.Text type="secondary">{space.description}</Typography.Text>}
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={onCreatePage}>
          {t('notes.newPage')}
        </Button>
      </div>

      {!isLoading && tree.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('notes.empty')} />
      ) : (
        <div className="flex flex-col">
          <Typography.Text type="secondary" className="mb-2 text-xs font-semibold uppercase tracking-wider">
            {t('notes.pagesTitle')}
          </Typography.Text>
          <div className="overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}` }}>
            {tree.map((page, index) => (
              <button
                key={page.id}
                type="button"
                className="flex w-full cursor-pointer items-center gap-3 px-4 py-2.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
                style={{ borderTop: index ? `1px solid ${token.colorBorderSecondary}` : undefined }}
                onClick={() => onOpenPage(page.id)}
              >
                <span className="w-5 shrink-0 text-center">{page.icon ?? <FileTextOutlined />}</span>
                <Typography.Text className="flex-1 truncate">{page.title || t('notes.untitled')}</Typography.Text>
                {page.children.length > 0 && (
                  <Typography.Text type="secondary" className="text-xs">
                    {t('notes.subpageCount', { count: page.children.length })}
                  </Typography.Text>
                )}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
