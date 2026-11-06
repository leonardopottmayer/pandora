# Arquitetura do frontend

[Índice da documentação](../../README.md) · [English](../en/frontend.md)

`client-web` é o app de navegador de todos os módulos: React 19, TypeScript, Vite, Ant Design 6,
Tailwind, TanStack Query, react-router e i18next. O Pandora Desktop envolve o mesmo build
([cliente desktop](desktop-client.md)). Como as telas se parecem e se comportam está em
[Pandora's look](../../../client-web/DESIGN.md); esta página é como o código se organiza.

## Pastas

```
client-web/src/
  app/            providers, router, tema (theme.ts guarda toda decisão de cor)
  components/     UI usada por mais de um módulo (settings/: PageHeading, SettingsSection, SettingRow)
  i18n/           setup do i18next e locales/en.json, locales/pt-BR.json
  layouts/        AppLayout (cabeçalho, menu lateral) e navigation.tsx (as entradas do menu)
  lib/            auxiliares do app; lib/api tem o cliente HTTP e o envelope do Tars
  modules/<nome>/ uma pasta por módulo do backend
  pages/          telas que não pertencem a módulo (Home, NotFound)
  test/           setup de testes, servidor msw, renderWithProviders
```

Dentro de um módulo, uma pasta por tipo de coisa:

| Pasta | Guarda | Exemplo |
|---|---|---|
| `models.ts` | Espelhos dos DTOs e requests do backend, e as listas de valores dos enums | `TransactionDto`, `ACCOUNT_TYPES` |
| `services/` | Um arquivo por recurso do backend, funções async simples sobre o `apiClient` | `transactions.service.ts` |
| `hooks/` | Hooks do TanStack Query sobre os services, e o `queryKeys.ts` | `useTransactions.ts` |
| `lib/` | Lógica pura: formatação, metadados de enum, cálculos, com testes ao lado | `format.ts`, `recurrence.ts` |
| `components/` | UI reaproveitada por várias telas do módulo | `TransactionDetailDrawer.tsx` |
| `pages/<área>/` | Telas de rota e os modais que só elas abrem | `pages/transactions/TransactionsListPage.tsx` |

Um componente usado por uma tela fica ao lado dela em `pages/<área>/`. Vai para `components/` do
módulo quando uma segunda tela precisa dele, e para `src/components/` quando um segundo módulo precisa.

## Conversando com o backend

- **`apiClient`** (`lib/api/client.ts`) é a única instância do axios. Ele põe o token bearer e o
  `Accept-Language` (para os erros do backend virem na língua do usuário), desembrulha o envelope de
  sucesso do Tars para `data` ser o payload, renova o token uma vez num 401 para todas as requisições
  que esperam por ele, e transforma um envelope de erro num `ApiResponseError`.
- **Services** recebem e devolvem os tipos do `models.ts` e conhecem a rota; mais ninguém conhece.
  ```ts
  const BASE = '/api/v1.0/finances/accounts'

  export async function getAccount(id: string): Promise<AccountDto> {
    const { data } = await apiClient.get<AccountDto>(`${BASE}/${id}`)
    return data
  }
  ```
- **Erros** chegam ao usuário por `toErrorMessage(err, fallback)`: a mensagem localizada do backend
  quando houver, o texto padrão da tela senão.
- **Buscar tudo.** Endpoints de listagem limitam o `take` (Finances: 200; um valor maior cai no padrão
  de 50, sem erro). Uma tela que mostra um período inteiro carrega todas as páginas com
  `fetchAllPages` (`modules/finances/services/transactions.service.ts`), que pede páginas de 200 até
  uma vir incompleta. Nunca passe um `take` grande achando que foi atendido.

## Estado do servidor

- Dados do servidor vivem no TanStack Query, nunca copiados para estado do componente. A tela deriva o
  que mostra (filtros, totais, grupos) dos dados da query com `useMemo`.
- **Query keys** vêm da fábrica do módulo (`hooks/queryKeys.ts`, ex.: `financeKeys`), montadas do geral
  para o específico, para uma área inteira poder ser invalidada de uma vez:
  ```ts
  accounts: () => [...financeKeys.all, 'accounts'] as const,
  account: (id: string) => [...financeKeys.accounts(), 'detail', id] as const,
  ```
  Hook nenhum escreve uma key à mão.
- **Mutations** invalidam, no `onSuccess`, toda key cujos dados elas mudam, inclusive de outras áreas
  (uma transação muda as transações, os saldos das contas, os cartões e as faturas). Os testes de hook
  conferem isso com `vi.spyOn(client, 'invalidateQueries')` no client do `createHookWrapper`.
- Várias queries do mesmo tipo (um saldo por conta) usam `useQueries` com as mesmas keys do hook de um
  item, para dividirem o cache dele.

## Rotas e navegação

- As rotas ficam em `app/router.tsx`. Toda tela protegida é lazy, então baixa quando é aberta pela
  primeira vez:
  `{ path: 'finances/accounts', lazy: page(() => import('@/modules/finances/pages/accounts/AccountsListPage'), 'AccountsListPage') }`.
  As telas são exports nomeados.
- As rotas são em inglês e são o que o navegador mostra (`/finances/recurring`).
- As entradas do menu lateral ficam em `layouts/navigation.tsx`. Uma tela nova precisa dos dois. Cada
  entrada declara a `feature` e a `permission` de que precisa: o menu a esconde e a rota mostra a página
  404/403 quando falta ao usuário (`layouts/navigationAccess.ts`). Botões usam `usePermission` ou
  `<Can>` de `modules/identity`.

## Idioma

- Todo texto que o usuário vê é uma key em **ambos** `en.json` e `pt-BR.json`, no namespace do módulo
  (`finances.transactions.new`). Palavras compartilhadas ficam em `common.*`.
- Plurais: `key_one` / `key_other`, chamados com `count`. Interpolação: `{{name}}`.
- Inglês é o padrão e o fallback; a escolha do usuário fica salva no aparelho e nas preferências.
- Código, nomes de arquivo, rotas e keys em inglês. Comentários podem ser em português.

## Testes

Vitest com jsdom, Testing Library e msw. Rode `npm test` (ou `npx vitest run <caminho>`).

- **Renderize** telas e componentes com `renderWithProviders` (`test/utils.tsx`): Query client sem
  retries, router, `App` do antd, i18n e um contexto de preferências fixo. Hooks usam
  `createHookWrapper`.
- **Idioma:** os testes mudam para inglês no `beforeAll` (`await i18n.changeLanguage('en')`) e acham os
  elementos pelo texto em inglês e pelo papel.
- **HTTP:** o servidor msw começa vazio e falha em qualquer requisição sem handler. Cada teste registra
  os handlers de que precisa com `server.use(...)`; monte-os com `FINANCES_BASE` e afins de
  `test/constants.ts`. Dentro de uma mesma chamada `server.use(...)` o **primeiro** handler que casa
  vence, então ponha o handler específico do teste antes de um genérico da mesma rota.
- **O que testar:** lógica pura em `lib/` ganha teste unitário (`recurrence.test.ts`); um service, teste
  da URL e do corpo; um hook, da key e das invalidações; uma tela, do comportamento que o usuário
  notaria (um campo obrigatório, um valor padrão, o que um menu faz, o que é enviado).
- **Largura da tela:** o antd lê os breakpoints pelo `matchMedia`, que o setup responde `false` para
  toda query, ou seja, um celular. Um teste do layout de desktop simula que as queries `min-width`
  casam (`asDesktop()` em `MailPage.test.tsx`), e um teste de celular deixa como está.

Peculiaridades do jsdom já encontradas:

- O antd esconde o input nativo dos radio buttons com `pointer-events: none`: clique no texto do label
  (`user.click(screen.getByText('Card'))`), ou use `userEvent.setup({ pointerEventsCheck: 0 })`.
- Itens de menu de Dropdown têm o papel `menuitem`.
- Um `tooltip` no `Form.Item` entra no nome acessível do campo; ache o campo com regex
  (`{ name: /Max occurrences/ }`).
- `form.isFieldTouched` pode ficar true depois de `setFieldValue`; não use para saber se o usuário digitou.
- "Not implemented: getComputedStyle with pseudo-elements" na saída é ruído do jsdom, não falha.

## Checagens antes de dar uma mudança por pronta

A partir de `client-web/`:

```bash
npx vitest run src/modules/<módulo>
```

```bash
npx tsc -b --noEmit
```

```bash
npx eslint src/modules/<módulo>
```

```bash
npx antd lint <arquivos .tsx alterados> --format json
```

Rode a suíte inteira (`npm test`) quando a mudança mexe em algo compartilhado (`src/components`, `lib`,
`app`, `layouts`, `test`). `npm run build` também checa tipos e gera o bundle.
