import { describe, expect, it } from 'vitest'
import { countCategories, filterCategoryTree } from './categoryTree'

const categories = [{
  id: 'food', name: 'Alimentação', nature: 'expense' as const,
  children: [
    { id: 'market', name: 'Mercado', nature: 'expense' as const, children: [] },
    { id: 'lunch', name: 'Almoço', nature: 'expense' as const, children: [] },
  ],
}]

describe('category search', () => {
  it('ignores accents and case and includes every child when the parent matches', () => {
    const result = filterCategoryTree(categories, ' ALIMENTACAO ')
    expect(result).toEqual(categories)
    expect(countCategories(result)).toBe(3)
  })

  it('retains the parent of a matching child and respects the nature filter', () => {
    const result = filterCategoryTree(categories, 'almoco', 'expense')
    expect(result[0].id).toBe('food')
    expect(result[0].children.map((category) => category.id)).toEqual(['lunch'])
    expect(countCategories(result)).toBe(2)
    expect(filterCategoryTree(categories, '', 'income')).toEqual([])
  })
})
