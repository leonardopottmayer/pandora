// Central query key factory for the integrations module.
export const integrationsKeys = {
  all: ['integrations'] as const,
  providers: () => [...integrationsKeys.all, 'providers'] as const,
  accounts: () => [...integrationsKeys.all, 'accounts'] as const,
  events: (limit: number) => [...integrationsKeys.all, 'events', limit] as const,
}
