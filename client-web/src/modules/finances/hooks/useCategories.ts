import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { CreateUserCategoryRequest, SystemCategoryDto, UpdateUserCategoryRequest, UserCategoryDto } from '../models'
import * as categoriesService from '../services/categories.service'
import type { SystemCategoriesParams } from '../services/categories.service'

export function useSystemCategories(params: SystemCategoriesParams = {}) {
  return useQuery({
    queryKey: financeKeys.systemCategories(params),
    queryFn: () => categoriesService.listSystemCategories(params),
    staleTime: 5 * 60_000, // árvore de sistema muda raramente
  })
}

export function useUserCategories(includeInactive = false) {
  return useQuery({
    queryKey: financeKeys.userCategories({ includeInactive }),
    queryFn: () => categoriesService.listUserCategories(includeInactive),
  })
}

function flattenCategoryNames(categories: Array<SystemCategoryDto | UserCategoryDto>, map: Map<string, string>) {
  for (const category of categories) {
    map.set(category.id, category.name)
    if (category.children.length) flattenCategoryNames(category.children, map)
  }
}

/** Mapa id -> nome, combinando categorias de sistema e do usuário (incl. subcategorias). */
export function useCategoryNames(): Map<string, string> {
  const { data: system } = useSystemCategories()
  const { data: user } = useUserCategories()

  return useMemo(() => {
    const map = new Map<string, string>()
    flattenCategoryNames(system ?? [], map)
    flattenCategoryNames(user ?? [], map)
    return map
  }, [system, user])
}

function useInvalidateCategories() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: financeKeys.categories() })
}

export function useCreateUserCategory() {
  const invalidate = useInvalidateCategories()
  return useMutation({
    mutationFn: (body: CreateUserCategoryRequest) => categoriesService.createUserCategory(body),
    onSuccess: invalidate,
  })
}

export function useUpdateUserCategory() {
  const invalidate = useInvalidateCategories()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateUserCategoryRequest }) =>
      categoriesService.updateUserCategory(id, body),
    onSuccess: invalidate,
  })
}

export function useSetUserCategoryActive() {
  const invalidate = useInvalidateCategories()
  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      categoriesService.setUserCategoryActive(id, active),
    onSuccess: invalidate,
  })
}
