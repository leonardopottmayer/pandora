import { useContext } from 'react'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AuthContext } from '@/modules/identity/context/auth-context'
import { adminKeys } from './queryKeys'
import * as adminService from '../services/admin.service'
import type { RoleRequest, UserException } from '../models'

export function useCatalog() {
  return useQuery({ queryKey: adminKeys.catalog(), queryFn: adminService.getCatalog, staleTime: Infinity })
}

export function useFeatures() {
  return useQuery({ queryKey: adminKeys.features(), queryFn: adminService.listFeatures })
}

export function useRoles(enabled = true) {
  return useQuery({ queryKey: adminKeys.roles(), queryFn: adminService.listRoles, enabled })
}

export function useAdminUsers() {
  return useQuery({ queryKey: adminKeys.users(), queryFn: adminService.listUsers })
}

export function useAdminUser(id: string | null) {
  return useQuery({
    queryKey: adminKeys.user(id ?? ''),
    queryFn: () => adminService.getUser(id!),
    enabled: !!id,
  })
}

const AUDIT_PAGE = 100

/** Newest first; each next page starts before the oldest entry loaded. */
export function useAudit(targetType: string) {
  return useInfiniteQuery({
    queryKey: adminKeys.audit(targetType),
    queryFn: ({ pageParam }) => adminService.listAudit({ targetType, before: pageParam }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => (last.length < AUDIT_PAGE ? undefined : last[last.length - 1].occurredAt),
  })
}

/**
 * Any change in the panel: every admin query is refetched (the audit log too), and so is `/me`,
 * since the administrator may have changed their own access.
 */
function useAdminMutation<TArgs, TResult>(mutationFn: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient()
  const reloadUser = useContext(AuthContext)?.reloadUser
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminKeys.all })
      void reloadUser?.()
    },
  })
}

export function useSetFeature() {
  return useAdminMutation(({ key, enabled }: { key: string; enabled: boolean }) => adminService.setFeature(key, enabled))
}

export function useCreateRole() {
  return useAdminMutation((body: RoleRequest) => adminService.createRole(body))
}

export function useUpdateRole() {
  return useAdminMutation(({ id, body }: { id: string; body: RoleRequest }) => adminService.updateRole(id, body))
}

export function useDeleteRole() {
  return useAdminMutation((id: string) => adminService.deleteRole(id))
}

export function useChangeUserRoles() {
  return useAdminMutation(({ userIds, roleId, assign }: { userIds: string[]; roleId: string; assign: boolean }) =>
    adminService.changeUserRoles(userIds, roleId, assign),
  )
}

export function useSetUserExceptions() {
  return useAdminMutation(({ id, exceptions }: { id: string; exceptions: UserException[] }) =>
    adminService.setUserExceptions(id, exceptions),
  )
}

export function useSetUserDisabled() {
  return useAdminMutation(({ id, disabled }: { id: string; disabled: boolean }) => adminService.setUserDisabled(id, disabled))
}

export function useRevokeUserSessions() {
  return useAdminMutation((id: string) => adminService.revokeUserSessions(id))
}
