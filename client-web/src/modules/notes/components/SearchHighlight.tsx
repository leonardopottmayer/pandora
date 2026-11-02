import { theme } from 'antd'
import { searchHighlights } from '../lib/searchHighlights'

/** Result text stays React text, including any HTML the note happens to contain. */
export function SearchHighlight({ text, term }: { text: string; term: string }) {
  const { token } = theme.useToken()
  return searchHighlights(text, term).map((part, index) =>
    part.matched ? (
      <mark key={index} style={{ padding: 0, background: token.colorPrimaryBg, color: token.colorPrimaryText }}>
        {part.text}
      </mark>
    ) : part.text,
  )
}
