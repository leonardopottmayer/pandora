import { describe, it, expect, beforeAll } from 'vitest'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import { CurrencyAmount } from './CurrencyAmount'

beforeAll(async () => {
  await i18n.changeLanguage('pt-BR')
})

describe('CurrencyAmount', () => {
  it('renders the formatted amount', () => {
    renderWithProviders(<CurrencyAmount amount={100} currency="BRL" />)
    expect(screen.getByText(/R\$/)).toBeInTheDocument()
  })

  it('prefixes a plus sign for inflow', () => {
    renderWithProviders(<CurrencyAmount amount={100} currency="BRL" direction="in" />)
    expect(screen.getByText(/^\+/)).toBeInTheDocument()
  })

  it('prefixes a minus sign for outflow', () => {
    renderWithProviders(<CurrencyAmount amount={100} currency="BRL" direction="out" />)
    expect(screen.getByText(/^−/)).toBeInTheDocument()
  })
})
