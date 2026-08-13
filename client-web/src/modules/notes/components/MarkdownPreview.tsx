import { useEffect, useMemo, useRef } from 'react'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { fetchAttachmentBlob } from '../services/attachments.service'

interface MarkdownPreviewProps {
  markdown: string
}

const ATTACHMENT_PREFIX = '/api/v1/notes/attachments/'

function renderHtml(md: string): string {
  const raw = marked.parse(md, { async: false }) as string
  return DOMPurify.sanitize(raw)
}

/**
 * Renders markdown to HTML. Embedded attachment images point at an authenticated endpoint that a
 * plain `<img src>` can't reach (the Bearer token lives in localStorage, not a cookie), so after
 * each render we fetch those blobs through the API client and swap in object URLs.
 */
export function MarkdownPreview({ markdown: md }: MarkdownPreviewProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const html = useMemo(() => renderHtml(md), [md])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const objectUrls: string[] = []
    let cancelled = false

    const images = container.querySelectorAll<HTMLImageElement>('img')
    images.forEach((img) => {
      // getAttribute keeps the original path; img.src would already be absolute.
      const src = img.getAttribute('src') ?? ''
      if (!src.includes(ATTACHMENT_PREFIX)) return
      const path = src.slice(src.indexOf(ATTACHMENT_PREFIX))
      void fetchAttachmentBlob(path)
        .then((blob) => {
          if (cancelled) return
          const objectUrl = URL.createObjectURL(blob)
          objectUrls.push(objectUrl)
          img.src = objectUrl
        })
        .catch(() => {
          /* leave the broken image; the source path stays visible for debugging */
        })
    })

    return () => {
      cancelled = true
      for (const url of objectUrls) URL.revokeObjectURL(url)
    }
  }, [html])

  return (
    <div
      ref={containerRef}
      className="notes-preview"
      // Sanitized above with DOMPurify.
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
