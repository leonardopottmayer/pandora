import type { TransactionNature } from '../../models'

interface CategoryNode<T> {
  id: string
  name: string
  nature: TransactionNature
  children: T[]
}

function searchText(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase().trim()
}

/** Keep matching descendants with their parent; a matching parent includes its whole group. */
export function filterCategoryTree<T extends CategoryNode<T>>(categories: T[], text: string, nature?: TransactionNature): T[] {
  const query = searchText(text)
  return categories.flatMap((category) => {
    if (nature && category.nature !== nature) return []
    if (!query || searchText(category.name).includes(query)) return [category]
    const children = filterCategoryTree(category.children, text, nature)
    return children.length ? [{ ...category, children }] : []
  })
}

export function countCategories<T extends CategoryNode<T>>(categories: T[]): number {
  return categories.reduce((count, category) => count + 1 + countCategories(category.children), 0)
}
