import { useState, type ReactNode } from 'react'
import { Alert, App, Button, Checkbox, Drawer, Input, Segmented, Space, Tag, Typography } from 'antd'
import { CloseOutlined } from '@ant-design/icons'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toErrorMessage } from '@/lib/api/envelope'
import { filesKeys, useFilesAgent } from '../hooks/useFiles'
import { mixedBelow, modeAt, toggle } from '../lib/selection'
import * as filesService from '../services/files.service'
import type { AgentFolder, Mark, Root, SelectionMode } from '../models'
import { LazyLevel, TreeRow } from './LazyTree'

interface Folder {
  path: string
  name: string
}

interface TreeContext {
  queryKey: readonly unknown[]
  load: (path: string) => Promise<Folder[]>
  checkbox: (path: string, label: string) => ReactNode
}

/** A level of the folder tree. Declared outside the drawer so a re-render keeps which folders are open. */
function Level({ tree, parent, depth }: { tree: TreeContext; parent: string; depth: number }) {
  return (
    <LazyLevel queryKey={[...tree.queryKey, parent]} load={() => tree.load(parent)} depth={depth}>
      {(folder: Folder) => (
        <TreeRow
          key={folder.path}
          depth={depth}
          expandable
          label={tree.checkbox(folder.path, folder.name)}
          renderChildren={() => <Level tree={tree} parent={folder.path} depth={depth + 1} />}
        />
      )}
    </LazyLevel>
  )
}

/**
 * The root's selection as a checkbox tree (product-plan §4.3). On the PC that has the root, the tree is
 * the live disk (`files.listFolders`); elsewhere it is what the catalog knows, and a path can be typed.
 */
export function SelectionDrawer({ root, onClose }: { root: Root; onClose: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const { agent, isHere } = useFilesAgent()
  const [marks, setMarks] = useState<Mark[]>(root.marks)
  const [typedPath, setTypedPath] = useState('')
  const [typedMode, setTypedMode] = useState<SelectionMode>('include')
  const cs = root.caseSensitive
  const live = isHere(root)

  async function loadFolders(path: string): Promise<Folder[]> {
    if (live) {
      const folders = await agent!.invoke<AgentFolder[]>('files.listFolders', { rootId: root.id, path })
      return folders.map((f) => ({ path: f.catalogPath!, name: f.name }))
    }
    // ponytail: the first page of the catalog's children (folders come first, 100 of them); add paging if a folder holds more.
    const page = await filesService.browse(root.id, path)
    return page.items.filter((e) => e.kind === 'directory').map((e) => ({ path: e.relativePath, name: e.name }))
  }

  const save = useMutation({
    mutationFn: () => filesService.saveSelection(root.id, marks),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: filesKeys.all })
      message.success(t('files.selection.saved'))
      onClose()
    },
    onError: (e) => message.error(toErrorMessage(e, t('files.selection.saveError'))),
  })

  function checkbox(path: string, label: string) {
    const included = modeAt(marks, path, cs) === 'include'
    return (
      <Checkbox
        checked={included}
        indeterminate={mixedBelow(marks, path, cs)}
        onChange={() => setMarks(toggle(marks, path, cs))}
      >
        {label}
      </Checkbox>
    )
  }

  const tree: TreeContext = { queryKey: [...filesKeys.all, 'folders', root.id, live], load: loadFolders, checkbox }

  function addTyped() {
    const path = '/' + typedPath.trim().replace(/\\/g, '/').replace(/^\/+|\/+$/g, '')
    setMarks([...marks.filter((m) => m.path !== path), { path, mode: typedMode }])
    setTypedPath('')
  }

  return (
    <Drawer
      open
      size={560}
      title={t('files.selection.title', { name: root.name })}
      onClose={onClose}
      extra={
        <Button type="primary" loading={save.isPending} onClick={() => save.mutate()}>
          {t('common.save')}
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        <Typography.Text type="secondary">
          {live ? t('files.selection.introLive') : t('files.selection.introCatalog')}
        </Typography.Text>

        <div>
          <TreeRow
            depth={0}
            expandable
            defaultOpen
            label={checkbox('/', root.name)}
            renderChildren={() => <Level tree={tree} parent="/" depth={1} />}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Typography.Text strong>{t('files.selection.marks')}</Typography.Text>
          {marks.length === 0 ? (
            <Typography.Text type="secondary">{t('files.selection.noMarks')}</Typography.Text>
          ) : (
            [...marks]
              .sort((a, b) => a.path.localeCompare(b.path))
              .map((m) => (
                <div key={m.path} className="flex items-center gap-2">
                  <Tag color={m.mode === 'include' ? 'green' : 'red'}>{t(`files.selection.${m.mode}`)}</Tag>
                  <Typography.Text code>{m.path}</Typography.Text>
                  <Button
                    type="text"
                    size="small"
                    aria-label={t('common.delete')}
                    icon={<CloseOutlined />}
                    onClick={() => setMarks(marks.filter((x) => x !== m))}
                  />
                </div>
              ))
          )}
          <Space.Compact>
            <Segmented<SelectionMode>
              value={typedMode}
              onChange={setTypedMode}
              options={[
                { value: 'include', label: t('files.selection.include') },
                { value: 'exclude', label: t('files.selection.exclude') },
              ]}
            />
            <Input
              placeholder="/Movies/Series"
              value={typedPath}
              onChange={(e) => setTypedPath(e.target.value)}
              onPressEnter={() => typedPath.trim() && addTyped()}
            />
            <Button disabled={!typedPath.trim()} onClick={addTyped}>
              {t('files.selection.addMark')}
            </Button>
          </Space.Compact>
        </div>

        <Alert type="info" showIcon title={t('files.selection.nextScan')} />
      </div>
    </Drawer>
  )
}
