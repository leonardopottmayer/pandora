import { useTranslation } from 'react-i18next'
import { Button, Collapse, Flex, Typography, theme } from 'antd'
import { FileTextOutlined, LinkOutlined } from '@ant-design/icons'
import { useBacklinks } from '../hooks/usePages'

interface BacklinksPanelProps {
  pageId: string
  onSelect: (id: string) => void
}

/** "Linked mentions": the pages whose markdown points at the open one. Read-only — edges follow content. */
export function BacklinksPanel({ pageId, onSelect }: BacklinksPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { data: backlinks = [], isLoading } = useBacklinks(pageId)

  return (
    <Collapse
      ghost
      size="small"
      style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}
      items={[
        {
          key: 'backlinks',
          label: (
            <Flex align="center" gap={8}>
              <LinkOutlined />
              <Typography.Text strong>{t('notes.backlinks')}</Typography.Text>
              <Typography.Text type="secondary">{isLoading ? '' : backlinks.length}</Typography.Text>
            </Flex>
          ),
          children:
            backlinks.length === 0 ? (
              <Typography.Text type="secondary">{t('notes.backlinksEmpty')}</Typography.Text>
            ) : (
              <Flex vertical align="start" gap={2}>
                {backlinks.map((backlink) => (
                  <Button
                    key={`${backlink.pageId}-${backlink.kind}`}
                    type="link"
                    size="small"
                    icon={backlink.icon ? <span>{backlink.icon}</span> : <FileTextOutlined />}
                    onClick={() => onSelect(backlink.pageId)}
                    style={{ opacity: backlink.isArchived ? 0.5 : 1 }}
                  >
                    {backlink.title || t('notes.untitled')}
                    {backlink.kind === 'embed' && (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {' '}
                        · {t('notes.embed')}
                      </Typography.Text>
                    )}
                  </Button>
                ))}
              </Flex>
            ),
        },
      ]}
    />
  )
}
