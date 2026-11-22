import { describe, expect, it } from 'vitest'
import { searchHighlights } from './searchHighlights'

describe('searchHighlights', () => {
  it('marks whole words matching any typed prefix, case insensitively', () => {
    expect(searchHighlights('Planejamento: VIAGEM e viagens', 'plane viag')).toEqual([
      { text: 'Planejamento', matched: true },
      { text: ': ', matched: false },
      { text: 'VIAGEM', matched: true },
      { text: ' e ', matched: false },
      { text: 'viagens', matched: true },
    ])
  })

  it('does not highlight a substring in the middle of a word', () => {
    expect(searchHighlights('destrabalho trabalho', 'traba')).toEqual([
      { text: 'destrabalho ', matched: false }, { text: 'trabalho', matched: true },
    ])
  })

  it('keeps accents and treats query punctuation as separators', () => {
    expect(searchHighlights('Reunião 2026', 'REUNI | 202:*').filter((part) => part.matched).map((part) => part.text))
      .toEqual(['Reunião', '2026'])
    expect(searchHighlights('ação', 'acao')).toEqual([{ text: 'ação', matched: false }])
  })

  it.each(['', '!!!'])('leaves text intact for an empty search: %s', (term) => {
    expect(searchHighlights('<b>literal</b>', term)).toEqual([{ text: '<b>literal</b>', matched: false }])
  })
})
