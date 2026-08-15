import { useTranslation } from 'react-i18next'
import { App, ColorPicker, Flex, Select, Typography } from 'antd'
import { TagsOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useSetTagColor, useTags } from '../hooks/useTags'

interface TagFilterProps {
  value: string[]
  onChange: (tagIds: string[]) => void
  /** Shown when nothing is picked. */
  placeholder?: string
}

/**
 * Picks the tags to narrow a view by — several tags **intersect**, so a page has to carry all of
 * them. The same dropdown is where a tag gets painted: the color is the one thing about a tag that
 * does not come from the pages' markdown, so this is the only place it can be set.
 */
export function TagFilter({ value, onChange, placeholder }: TagFilterProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { tags, isLoading } = useTags()
  const colorMutation = useSetTagColor()

  async function handleColor(id: string, color: string | null) {
    try {
      await colorMutation.mutateAsync({ id, color })
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  return (
    <Select
      mode="multiple"
      allowClear
      size="small"
      loading={isLoading}
      value={value}
      onChange={onChange}
      placeholder={placeholder ?? t('notes.tagFilter')}
      suffixIcon={<TagsOutlined />}
      style={{ width: '100%' }}
      // The label is a plain string so the closable chips of the picked tags stay readable.
      optionLabelProp="label"
      options={tags.map((tag) => ({
        value: tag.id,
        label: tag.name,
        title: tag.name,
      }))}
      optionRender={(option) => {
        const tag = tags.find((candidate) => candidate.id === option.value)
        if (!tag) return option.label
        return (
          <Flex align="center" gap={8}>
            {/* The picker lives inside an option: opening it must not pick the tag as well. */}
            <span onClick={(event) => event.stopPropagation()} style={{ display: 'flex' }}>
              <ColorPicker
                size="small"
                value={tag.color ?? undefined}
                allowClear
                onChangeComplete={(color) => void handleColor(tag.id, color.toHexString())}
                onClear={() => void handleColor(tag.id, null)}
              />
            </span>
            <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis' }}>{tag.name}</span>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {tag.pageCount}
            </Typography.Text>
          </Flex>
        )
      }}
    />
  )
}
