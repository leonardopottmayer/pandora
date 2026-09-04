// Central query key factory for the assistant module.
export const assistantKeys = {
  all: ['assistant'] as const,
  profile: () => [...assistantKeys.all, 'profile'] as const,
  providers: () => [...assistantKeys.all, 'providers'] as const,
}
