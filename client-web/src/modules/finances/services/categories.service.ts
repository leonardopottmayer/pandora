import { apiClient } from '@/lib/api/client'
import type {
  CreateUserCategoryRequest,
  SystemCategoryDto,
  TransactionNature,
  UpdateUserCategoryRequest,
  UserCategoryDto,
} from '../models'

const SYSTEM_BASE = '/api/v1.0/finances/categories/system'
const USER_BASE = '/api/v1.0/finances/categories'

export interface SystemCategoriesParams {
  nature?: TransactionNature
  includeInactive?: boolean
}

export async function listSystemCategories(
  params: SystemCategoriesParams = {},
): Promise<SystemCategoryDto[]> {
  const { data } = await apiClient.get<SystemCategoryDto[]>(SYSTEM_BASE, {
    params: { nature: params.nature, includeInactive: params.includeInactive },
  })
  return data
}

export async function listUserCategories(includeInactive = false): Promise<UserCategoryDto[]> {
  const { data } = await apiClient.get<UserCategoryDto[]>(USER_BASE, {
    params: { includeInactive },
  })
  return data
}

export async function createUserCategory(
  body: CreateUserCategoryRequest,
): Promise<UserCategoryDto> {
  const { data } = await apiClient.post<UserCategoryDto>(USER_BASE, body)
  return data
}

export async function updateUserCategory(
  id: string,
  body: UpdateUserCategoryRequest,
): Promise<UserCategoryDto> {
  const { data } = await apiClient.put<UserCategoryDto>(`${USER_BASE}/${id}`, body)
  return data
}

export async function setUserCategoryActive(id: string, active: boolean): Promise<UserCategoryDto> {
  const action = active ? 'activate' : 'deactivate'
  const { data } = await apiClient.post<UserCategoryDto>(`${USER_BASE}/${id}/${action}`)
  return data
}
