import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Card, Empty, Flex, Spin, Typography } from 'antd'
import { NotesSidebar } from '../components/NotesSidebar'
import { GraphView } from '../components/GraphView'
import { SearchPalette } from '../components/SearchPalette'
import { useGraph } from '../hooks/usePages'

const SIDEBAR_WIDTH = 280

/** The whole wiki graph. Clicking a node leaves for that page, which is the point of the view. */
export function NotesGraphPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [includeArchived, setIncludeArchived] = useState(false)
  // The sidebar here is the same one the editor page shows, search button included.
  const [searchOpen, setSearchOpen] = useState(false)

  const { data: graph, isLoading } = useGraph()

  function openPage(id: string | null) {
    navigate(id ? `/notes/${id}` : '/notes')
  }

  return (
    <Flex gap={16} align="stretch" style={{ height: 'calc(100vh - 112px)' }}>
      <SearchPalette open={searchOpen} onOpenChange={setSearchOpen} onSelect={openPage} />

      <Card
        styles={{ body: { padding: 12, height: '100%', overflow: 'hidden' } }}
        style={{ width: SIDEBAR_WIDTH, flexShrink: 0, height: '100%' }}
      >
        <NotesSidebar
          selectedId={null}
          onSelect={openPage}
          includeArchived={includeArchived}
          onToggleArchived={setIncludeArchived}
          onSearch={() => setSearchOpen(true)}
        />
      </Card>

      <Card
        title={<Typography.Text strong>{t('notes.graph')}</Typography.Text>}
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
            <Empty description={t('notes.graphEmpty')} />
          </Flex>
        )}
      </Card>
    </Flex>
  )
}
