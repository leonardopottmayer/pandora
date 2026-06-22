import { describe, it, expect, beforeAll } from 'vitest'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import { EnumTag } from './EnumTag'
import { TRANSACTION_STATUS_META } from '../lib/enums'

beforeAll(async () => {
  await i18n.changeLanguage('pt-BR')
})

describe('EnumTag', () => {
  it('renders nothing when meta is undefined', () => {
    const { container } = renderWithProviders(<EnumTag meta={undefined} />)
    expect(container.querySelector('.ant-tag')).toBeNull()
  })

  it('renders a tag with the translated label for a known enum', () => {
    const meta = TRANSACTION_STATUS_META.posted
    renderWithProviders(<EnumTag meta={meta} />)
    expect(screen.getByText(i18n.t(meta.labelKey))).toBeInTheDocument()
  })
})
