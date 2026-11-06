import { apiClient } from '@/lib/api/client'
import type {
  AdminUserDetailDto,
  AdminUserDto,
  AuditEntryDto,
  AuditFilter,
  CatalogModule,
  FeatureDto,
  RoleDto,
  RoleRequest,
  UserException,
} from '../models'

const BASE = '/api/v1.0/admin'

export async function getCatalog(): Promise<CatalogModule[]> {
  const { data } = await apiClient.get<CatalogModule[]>(`${BASE}/catalog`)
  return data
}

export async function listFeatures(): Promise<FeatureDto[]> {
  const { data } = await apiClient.get<FeatureDto[]>(`${BASE}/features`)
  return data
}

/** Returns every flag again, since switching a parent changes its children. */
export async function setFeature(key: string, enabled: boolean): Promise<FeatureDto[]> {
  const { data } = await apiClient.put<FeatureDto[]>(`${BASE}/features/${encodeURIComponent(key)}`, { enabled })
  return data
}

export async function listRoles(): Promise<RoleDto[]> {
  const { data } = await apiClient.get<RoleDto[]>(`${BASE}/roles`)
  return data
}

export async function createRole(body: RoleRequest): Promise<string> {
  const { data } = await apiClient.post<string>(`${BASE}/roles`, body)
  return data
}

export async function updateRole(id: string, body: RoleRequest): Promise<void> {
  await apiClient.put(`${BASE}/roles/${id}`, body)
}

export async function deleteRole(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/roles/${id}`)
}

export async function listUsers(): Promise<AdminUserDto[]> {
  const { data } = await apiClient.get<AdminUserDto[]>(`${BASE}/users`)
  return data
}

export async function getUser(id: string): Promise<AdminUserDetailDto> {
  const { data } = await apiClient.get<AdminUserDetailDto>(`${BASE}/users/${id}`)
  return data
}

/** Gives a role to (`assign`) or takes it from several users; returns how many changed. */
export async function changeUserRoles(userIds: string[], roleId: string, assign: boolean): Promise<number> {
  const { data } = await apiClient.post<number>(`${BASE}/users/roles`, { userIds, roleId, assign })
  return data
}

export async function setUserExceptions(id: string, exceptions: UserException[]): Promise<void> {
  await apiClient.put(`${BASE}/users/${id}/exceptions`, { exceptions })
}

export async function setUserDisabled(id: string, disabled: boolean): Promise<void> {
  await apiClient.put(`${BASE}/users/${id}/disabled`, { disabled })
}

export async function revokeUserSessions(id: string): Promise<void> {
  await apiClient.post(`${BASE}/users/${id}/revoke-sessions`)
}

export async function listAudit(filter: AuditFilter = {}): Promise<AuditEntryDto[]> {
  const { data } = await apiClient.get<AuditEntryDto[]>(`${BASE}/audit`, {
    params: { targetType: filter.targetType || undefined, before: filter.before, limit: 100 },
  })
  return data
}
