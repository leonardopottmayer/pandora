// Mirrors of the Admin module's DTOs (backend/src/Modules/Admin/...Application/Dtos).

export interface CatalogAction {
  /** Full key, `module.resource.action`. */
  key: string
  action: string
  /** Keys this one depends on (the resource's `read`). */
  requires: string[]
}

export interface CatalogResource {
  key: string
  /** The flag that gates the resource, if any. */
  feature: string | null
  actions: CatalogAction[]
}

export interface CatalogModule {
  key: string
  resources: CatalogResource[]
}

export interface FeatureDto {
  key: string
  parent: string | null
  defaultEnabled: boolean
  /** The administrator's choice, or null while the default applies. */
  override: boolean | null
  /** Effective: off whenever an ancestor is off. */
  enabled: boolean
}

export interface RoleDto {
  id: string
  name: string
  description: string | null
  isSystem: boolean
  isDefault: boolean
  patterns: string[]
  holders: number
}

export interface RoleRequest {
  name: string
  description: string | null
  patterns: string[]
}

export interface AdminUserDto {
  id: string
  name: string
  username: string
  email: string
  activated: boolean
  disabled: boolean
  /** Activated while approval was required, and not approved yet: cannot sign in. */
  awaitingApproval: boolean
  mfaEnabled: boolean
  lastSignInAt: string | null
  createdAt: string
  roleIds: string[]
}

export const EXCEPTION_EFFECTS = ['allow', 'deny'] as const
export type ExceptionEffect = (typeof EXCEPTION_EFFECTS)[number]

export interface UserException {
  pattern: string
  effect: ExceptionEffect
}

export interface EffectivePermission {
  key: string
  granted: boolean
  /** Names of the user's roles that grant it. */
  roles: string[]
  allowed: boolean
  denied: boolean
}

export interface AdminUserDetailDto {
  user: AdminUserDto
  exceptions: UserException[]
  permissions: EffectivePermission[]
}

export interface AuditEntryDto {
  id: string
  actorId: string | null
  actorName: string | null
  action: string
  targetType: 'user' | 'role' | 'feature'
  targetId: string
  targetName: string | null
  /** JSON. */
  before: string | null
  /** JSON. */
  after: string | null
  occurredAt: string
}

export interface AuditFilter {
  targetType?: string
  before?: string
}

/** The backend's answer when the caller's account has no two-step verification. */
export const MFA_REQUIRED = 'Admin.MfaRequired'
