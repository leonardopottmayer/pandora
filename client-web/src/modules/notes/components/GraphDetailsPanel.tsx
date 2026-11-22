import { useTranslation } from 'react-i18next'
import { Button, Card, Drawer, Flex, Tag, Typography } from 'antd'
import { CloseOutlined } from '@ant-design/icons'
import { buildAdjacency, nodeLabel } from '../lib/graphData'
import type { GraphFamily } from '../lib/graphGroups'
import type { GraphNodeDto, PageGraphDto, TagDto } from '../models'

interface GraphDetailsPanelProps {
  node: GraphNodeDto
  graph: PageGraphDto
  tags: TagDto[]
  families: GraphFamily[]
  compact: boolean
  onSelect: (id: string) => void
  onOpen: (id: string) => void
  onClose: () => void
}

/** Metadata and distinct neighboring pages from the currently visible graph. */
export function GraphDetailsPanel({ node, graph, tags, families, compact, onSelect, onOpen, onClose }: GraphDetailsPanelProps) {
  const { t } = useTranslation()
  const neighbors = buildAdjacency(graph).get(node.id)
  const connections = graph.nodes.filter((candidate) => candidate.id !== node.id && neighbors?.has(candidate.id))
    .sort((a, b) => a.title.localeCompare(b.title))
  const matchingFamilies = families.filter((family) => family.tagIds.some((id) => node.tagIds.includes(id)))
  const title = t('notes.graphDetails.title')
  const content = (
    <Flex vertical gap={16}>
      <div>
        <Typography.Title level={5} style={{ marginTop: 0, overflowWrap: 'anywhere' }}>
          {nodeLabel(node.icon, node.title) || t('notes.untitled')}
        </Typography.Title>
        {node.isArchived && <Tag>{t('notes.graphDetails.archived')}</Tag>}
      </div>
      <div>
        <Typography.Paragraph strong>{t('notes.graphDetails.tags')}</Typography.Paragraph>
        <Flex wrap gap={4}>
          {node.tagIds.length === 0 && <Typography.Text type="secondary">{t('notes.graphDetails.noTags')}</Typography.Text>}
          {node.tagIds.map((id) => {
            const tag = tags.find((candidate) => candidate.id === id)
            return <Tag key={id} color={tag?.color ?? undefined} style={{ whiteSpace: 'normal', overflowWrap: 'anywhere' }}>
              {tag ? `#${tag.name}` : t('notes.graphGroups.missingTag')}
            </Tag>
          })}
        </Flex>
      </div>
      <div>
        <Typography.Paragraph strong>{t('notes.graphDetails.families')}</Typography.Paragraph>
        <Flex wrap gap={4}>
          {matchingFamilies.length === 0 && <Typography.Text type="secondary">{t('notes.graphGroups.ungrouped')}</Typography.Text>}
          {matchingFamilies.map((family) => <Tag key={family.id} color={family.color} style={{ whiteSpace: 'normal', overflowWrap: 'anywhere' }}>{family.name}</Tag>)}
        </Flex>
        {matchingFamilies.length > 1 && <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
          {t('notes.graphDetails.region', { name: matchingFamilies[0].name })}
        </Typography.Paragraph>}
      </div>
      <div>
        <Typography.Paragraph strong>{t('notes.graphDetails.connections', { count: connections.length })}</Typography.Paragraph>
        {connections.length === 0 && <Typography.Text type="secondary">{t('notes.graphDetails.noConnections')}</Typography.Text>}
        <Flex vertical align="start" gap={4}>
          {connections.map((connection) => <Button
            key={connection.id}
            type="link"
            onClick={() => onSelect(connection.id)}
            style={{ height: 'auto', whiteSpace: 'normal', textAlign: 'start', maxWidth: '100%' }}
          >
            <span style={{ overflowWrap: 'anywhere' }}>{nodeLabel(connection.icon, connection.title) || t('notes.untitled')}</span>
          </Button>)}
        </Flex>
      </div>
      <Button type="primary" block onClick={() => onOpen(node.id)}>{t('notes.graphDetails.open')}</Button>
    </Flex>
  )

  if (compact) return <Drawer open title={title} onClose={onClose} size={360} styles={{ wrapper: { maxWidth: '100vw' } }}>{content}</Drawer>

  return (
    <Card
      title={title}
      extra={<Button type="text" icon={<CloseOutlined />} aria-label={t('notes.graphDetails.close')} onClick={onClose} />}
      style={{ width: 320, flexShrink: 0, height: '100%', display: 'flex', flexDirection: 'column' }}
      styles={{ body: { overflowY: 'auto', minHeight: 0, flex: 1 } }}
    >{content}</Card>
  )
}
