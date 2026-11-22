import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Collapse, Flex, Segmented, Spin, Typography, theme } from 'antd'
import { PartitionOutlined } from '@ant-design/icons'
import { useLocalGraph } from '../hooks/usePages'
import { GraphView } from './GraphView'

interface LocalGraphPanelProps {
  pageId: string
  onSelect: (id: string) => void
}

/** How far out the neighborhood reaches; past three hops a personal notebook is mostly connected. */
const DEPTHS = [1, 2, 3]

const GRAPH_HEIGHT = 240

/**
 * The neighborhood of the open page, Obsidian-style. Collapsed by default: the graph is a canvas
 * running a simulation, and it should not be doing that under every page the user opens to read.
 */
export function LocalGraphPanel({ pageId, onSelect }: LocalGraphPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [depth, setDepth] = useState(1)

  const { data: graph, isLoading } = useLocalGraph(pageId, depth)

  return (
    <Collapse
      ghost
      size="small"
      style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}
      items={[
        {
          key: 'graph',
          label: (
            <Flex align="center" gap={8}>
              <PartitionOutlined />
              <Typography.Text strong>{t('notes.localGraph')}</Typography.Text>
            </Flex>
          ),
          extra: (
            <Segmented
              size="small"
              value={depth}
              onChange={(value) => setDepth(Number(value))}
              onClick={(e) => e.stopPropagation()}
              options={DEPTHS.map((value) => ({ value, label: String(value) }))}
              title={t('notes.graphDepth')}
            />
          ),
          children: isLoading ? (
            <Flex align="center" justify="center" style={{ height: GRAPH_HEIGHT }}>
              <Spin />
            </Flex>
          ) : (
            <div style={{ height: GRAPH_HEIGHT }}>
              <GraphView graph={graph} currentId={pageId} onSelect={onSelect} />
            </div>
          ),
        },
      ]}
    />
  )
}
