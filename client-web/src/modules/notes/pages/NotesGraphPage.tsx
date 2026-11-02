import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Button, Card, Empty, Flex, Grid, Select, Spin, Switch, Typography } from 'antd'
import { useAuth } from '@/modules/identity/context/auth-context'
import { NotesSidebar } from '../components/NotesSidebar'
import { GraphView } from '../components/GraphView'
import { SearchPalette } from '../components/SearchPalette'
import { useGraph } from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'
import { useTags } from '../hooks/useTags'
import { useGraphPreferences } from '../hooks/useGraphPreferences'
import { GraphGroupsDrawer } from '../components/GraphGroupsDrawer'
import { TagFilter } from '../components/TagFilter'
import { nodeLabel } from '../lib/graphData'

const SIDEBAR_WIDTH = 280

/** The whole wiki graph. Clicking a node leaves for that page, which is the point of the view. */
export function NotesGraphPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const screens = Grid.useBreakpoint()
  const { preferences, update } = useGraphPreferences(user?.id ?? null)
  const { tags } = useTags()
  const [groupsOpen, setGroupsOpen] = useState(false)
  const [includeArchived, setIncludeArchived] = useState(false)
  // The sidebar here is the same one the editor page shows, search button included.
  const [searchOpen, setSearchOpen] = useState(false)
  // One filter for both panes: picking a tag narrows the graph and the sidebar at once.
  const [tagIds, setTagIds] = useState<string[]>([])
  // The graph spans every space unless one is picked; the sidebar follows the same choice.
  const [spaceId, setSpaceId] = useState<string | null>(null)
  const [locatedId, setLocatedId] = useState<string | null>(null)
  const { data: spaces = [] } = useSpaces()

  const { data: graph, isLoading } = useGraph(tagIds, spaceId)
  const focusedId = graph?.nodes.some((node) => node.id === locatedId) ? locatedId : null

  function changeTags(ids: string[]) {
    setTagIds(ids)
    setLocatedId(null)
  }

  function changeSpace(id: string | null) {
    setSpaceId(id)
    setLocatedId(null)
  }

  function openPage(id: string | null) {
    navigate(id ? `/notes/${id}` : '/notes')
  }

  return (
    <Flex gap={16} align="stretch" style={{ height: 'calc(100vh - 112px)', minWidth: 0 }}>
      <SearchPalette open={searchOpen} onOpenChange={setSearchOpen} onSelect={openPage} spaceId={spaceId} />
      {groupsOpen && (
        <GraphGroupsDrawer
          families={preferences.families}
          tags={tags}
          onClose={() => setGroupsOpen(false)}
          onSave={(families) => update({ ...preferences, families })}
        />
      )}

      {screens.lg && (
        <Card
          styles={{ body: { padding: 12, height: '100%', overflow: 'hidden' } }}
          style={{ width: SIDEBAR_WIDTH, flexShrink: 0, height: '100%' }}
        >
          <NotesSidebar
            spaceId={spaceId}
            onSpaceChange={changeSpace}
            selectedId={null}
            onSelect={openPage}
            includeArchived={includeArchived}
            onToggleArchived={setIncludeArchived}
            tagIds={tagIds}
            onTagIdsChange={changeTags}
            onSearch={() => setSearchOpen(true)}
          />
        </Card>
      )}

      <Card
        title={
          <Flex wrap gap={8} align="center" style={{ padding: '10px 0' }}>
            <Typography.Text strong>{t('notes.graph')}</Typography.Text>
            <Select
              size="small"
              allowClear
              style={{ width: 240, maxWidth: '100%' }}
              value={focusedId ?? undefined}
              placeholder={t('notes.graphSearch.placeholder')}
              aria-label={t('notes.graphSearch.placeholder')}
              notFoundContent={t('notes.graphSearch.empty')}
              showSearch={{
                filterOption: (input, option) => {
                  const normalize = (value: string) => value.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase()
                  return normalize(option?.title ?? '').includes(normalize(input.trim()))
                },
              }}
              options={(graph?.nodes ?? []).map((node) => ({
                value: node.id, label: nodeLabel(node.icon, node.title), title: node.title,
              }))}
              onChange={(id) => setLocatedId(id ?? null)}
            />
            <label className="flex items-center gap-2">
              <Switch
                size="small"
                checked={preferences.showGroups}
                aria-label={t('notes.graphGroups.show')}
                onChange={(showGroups) => update({ ...preferences, showGroups })}
              />
              <Typography.Text>{t('notes.graphGroups.show')}</Typography.Text>
            </label>
            <Button size="small" onClick={() => setGroupsOpen(true)}>{t('notes.graphGroups.configure')}</Button>
            {!screens.lg && (
              <div style={{ minWidth: 160, flex: 1 }}>
                <TagFilter value={tagIds} onChange={changeTags} />
              </div>
            )}
            <Select
              size="small"
              className="min-w-44"
              value={spaceId ?? ''}
              onChange={(value) => changeSpace(value || null)}
              aria-label={t('notes.spaces.title')}
              options={[
                { value: '', label: t('notes.spaces.all') },
                ...spaces.map((candidate) => ({ value: candidate.id, label: candidate.name })),
              ]}
            />
          </Flex>
        }
        styles={{ body: { padding: 0, flex: 1, minHeight: 0 } }}
        style={{ flex: 1, minWidth: 0, height: '100%', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}
      >
        {isLoading ? (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Spin />
          </Flex>
        ) : graph && graph.nodes.length > 0 ? (
          <GraphView graph={graph} focusedId={focusedId} onSelect={openPage} families={preferences.showGroups ? preferences.families : undefined} />
        ) : (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Empty description={tagIds.length > 0 ? t('notes.tagFilterEmpty') : t('notes.graphEmpty')} />
          </Flex>
        )}
      </Card>
    </Flex>
  )
}
