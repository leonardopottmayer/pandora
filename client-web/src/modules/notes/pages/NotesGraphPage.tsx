import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Card, Empty, Flex, Select, Spin, Typography } from 'antd'
import { NotesSidebar } from '../components/NotesSidebar'
import { GraphView } from '../components/GraphView'
import { SearchPalette } from '../components/SearchPalette'
import { useGraph } from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'

const SIDEBAR_WIDTH = 280

/** The whole wiki graph. Clicking a node leaves for that page, which is the point of the view. */
export function NotesGraphPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [includeArchived, setIncludeArchived] = useState(false)
  // The sidebar here is the same one the editor page shows, search button included.
  const [searchOpen, setSearchOpen] = useState(false)
  // One filter for both panes: picking a tag narrows the graph and the sidebar at once.
  const [tagIds, setTagIds] = useState<string[]>([])
  // The graph spans every space unless one is picked; the sidebar follows the same choice.
  const [spaceId, setSpaceId] = useState<string | null>(null)
  const { data: spaces = [] } = useSpaces()

  const { data: graph, isLoading } = useGraph(tagIds, spaceId)

  function openPage(id: string | null) {
    navigate(id ? `/notes/${id}` : '/notes')
  }

  return (
    <Flex gap={16} align="stretch" style={{ height: 'calc(100vh - 112px)' }}>
      <SearchPalette open={searchOpen} onOpenChange={setSearchOpen} onSelect={openPage} spaceId={spaceId} />

      <Card
        styles={{ body: { padding: 12, height: '100%', overflow: 'hidden' } }}
        style={{ width: SIDEBAR_WIDTH, flexShrink: 0, height: '100%' }}
      >
        <NotesSidebar
          spaceId={spaceId}
          onSpaceChange={setSpaceId}
          selectedId={null}
          onSelect={openPage}
          includeArchived={includeArchived}
          onToggleArchived={setIncludeArchived}
          tagIds={tagIds}
          onTagIdsChange={setTagIds}
          onSearch={() => setSearchOpen(true)}
        />
      </Card>

      <Card
        title={<Typography.Text strong>{t('notes.graph')}</Typography.Text>}
        extra={
          <Select
            size="small"
            className="min-w-44"
            value={spaceId ?? ''}
            onChange={(value) => setSpaceId(value || null)}
            aria-label={t('notes.spaces.title')}
            options={[
              { value: '', label: t('notes.spaces.all') },
              ...spaces.map((candidate) => ({ value: candidate.id, label: candidate.name })),
            ]}
          />
        }
        styles={{ body: { padding: 0, height: 'calc(100% - 56px)' } }}
        style={{ flex: 1, height: '100%', overflow: 'hidden' }}
      >
        {isLoading ? (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Spin />
          </Flex>
        ) : graph && graph.nodes.length > 0 ? (
          <GraphView graph={graph} onSelect={openPage} />
        ) : (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Empty description={tagIds.length > 0 ? t('notes.tagFilterEmpty') : t('notes.graphEmpty')} />
          </Flex>
        )}
      </Card>
    </Flex>
  )
}
