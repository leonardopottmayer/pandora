// Central query key factory for the admin module.
export const adminKeys = {
  all: ['admin'] as const,
  catalog: () => [...adminKeys.all, 'catalog'] as const,
  features: () => [...adminKeys.all, 'features'] as const,
  roles: () => [...adminKeys.all, 'roles'] as const,
  users: () => [...adminKeys.all, 'users'] as const,
  user: (id: string) => [...adminKeys.users(), 'detail', id] as const,
  audit: (targetType: string) => [...adminKeys.all, 'audit', targetType] as const,
}
