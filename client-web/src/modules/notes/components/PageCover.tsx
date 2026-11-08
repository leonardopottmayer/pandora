import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Popover, Slider } from 'antd'
import { DeleteOutlined, DragOutlined, PictureOutlined } from '@ant-design/icons'
import { useMediaPathUrls } from '../hooks/useMediaUrls'
import { mediaPath } from '../lib/media'
import { COVER_GRADIENTS, type PageCover as Cover } from '../models'
import { MediaPickerModal } from './library/MediaPickerModal'

interface PageCoverProps {
  cover: Cover | null | undefined
  editable: boolean
  onChange: (cover: Cover | null) => void
}

/** The band across the top of a page: a library image or a gradient, moved up and down to frame it. */
export function PageCover({ cover, editable, onChange }: PageCoverProps) {
  const { t } = useTranslation()
  const [picking, setPicking] = useState(false)
  const path = cover?.mediaId ? mediaPath(cover.mediaId) : null
  const urls = useMediaPathUrls(path ? [path] : [])
  const src = path ? urls.get(path) : null
  if (!cover) return null

  const position = cover.position ?? 50
  return (
    <div className={`notes-cover ${cover.gradient ? `notes-cover-${cover.gradient}` : ''}`} role="img" aria-label={t('notes.cover.label')}>
      {src && <img src={src} alt="" style={{ objectPosition: `center ${position}%` }} />}
      {editable && (
        <div className="notes-cover-tools">
          <Popover
            trigger="click"
            content={
              <div className="notes-cover-picker">
                <div className="notes-cover-gradients">
                  {COVER_GRADIENTS.map((gradient) => (
                    <button
                      key={gradient}
                      type="button"
                      className={`notes-cover-${gradient}`}
                      aria-label={t(`notes.cover.gradients.${gradient}`)}
                      aria-pressed={cover.gradient === gradient}
                      onClick={() => onChange({ gradient, position: 50 })}
                    />
                  ))}
                </div>
                <Button size="small" icon={<PictureOutlined />} onClick={() => setPicking(true)}>{t('notes.rich.fromLibrary')}</Button>
              </div>
            }
          >
            <Button size="small">{t('notes.cover.change')}</Button>
          </Popover>
          {cover.mediaId && (
            <Popover
              trigger="click"
              content={
                <div className="w-48">
                  <Slider min={0} max={100} value={position} tooltip={{ formatter: null }} aria-label={t('notes.cover.reposition')} onChange={(value) => onChange({ ...cover, position: value })} />
                </div>
              }
            >
              <Button size="small" icon={<DragOutlined />} aria-label={t('notes.cover.reposition')} />
            </Popover>
          )}
          <Button size="small" icon={<DeleteOutlined />} aria-label={t('notes.cover.remove')} onClick={() => onChange(null)} />
        </div>
      )}
      <MediaPickerModal
        open={picking}
        kind="image"
        onClose={() => setPicking(false)}
        onPick={(item) => {
          setPicking(false)
          onChange({ mediaId: item.id, position: 50 })
        }}
      />
    </div>
  )
}
