# Módulo Admin — Plano de Produto

> **Status:** design aprovado (2026-11-06), nada construído ainda. Cobre os pacotes do Tars de que ele
> precisa (feature flags e autorização) e o módulo do Pandora que os usa.
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Identity](../../identity/pt-BR/README.md) · [Tars wiring](../../../architecture/pt-BR/tars-wiring.md)

---

## 1. O que o módulo faz

O **Admin** decide o que está ligado no sistema e o que cada usuário pode fazer, e dá ao
administrador master um lugar só para gerenciar isso quando o Pandora for publicado.

São dois eixos independentes, checados nesta ordem em toda requisição:

```
requisição ──► 1. A feature está ligada no sistema?        não ► 404 (ninguém, nem o admin)
                │ sim
                ▼
              2. O usuário tem a permissão da ação?         não ► 403
                │ sim
                ▼
              o handler roda
```

- **Feature flags** são interruptores globais: um módulo inteiro (`finances`) ou uma capacidade
  (`finances.import`). Não dependem de quem é o usuário.
- **Permissões** são por ação: `finances.cards.read`, `finances.cards.create`,
  `finances.cards.delete`, `finances.imports.run` e assim por diante. O usuário as recebe por
  **papéis** e por **exceções** individuais de permitir ou negar.

### O que ele não é

- **Não é autenticação.** Login, tokens, MFA e os próprios usuários continuam no
  [Identity](../../identity/pt-BR/README.md). O Admin só responde "isso é permitido?".
- **Não é dono dos dados.** Cada módulo continua filtrando os dados por `UserId`. A permissão diz que
  o usuário pode excluir *cartões*; nunca deixa ele mexer no cartão de outra pessoa.
- **Não é multitenancy.** Uma instalação, um conjunto de papéis. Permissões por tenant ficam fora.

---

## 2. Nomes e coordenadas

| Item | Valor |
|---|---|
| Pacotes do Tars | `Pottmayer.Tars.FeatureFlags{,.Abstractions,.AspNetCore}`, `Pottmayer.Tars.Security.Authorization{,.Abstractions,.AspNetCore,.EntityFrameworkCore}` |
| Projetos do backend | `Pottmayer.Pandora.Modules.Admin.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `admin` |
| Prefixo de tabela | `admXXX_`, PK `uuid_generate_v7()` |
| Seção de options | `Pandora:Admin` |
| Base da API | `/api/v{version}/admin`, JWT, cada endpoint protegido por uma permissão `admin.*` |
| Frontend | `client-web/src/modules/admin`, rotas `admin/users`, `admin/roles`, `admin/features`, `admin/audit` |
| Migrations | `migrations/migrations/admin/` |

---

## 3. Princípios

1. **Declarado no código, alterado no banco.** Flags e permissões são declaradas pelo módulo dono,
   com seus padrões. O banco guarda só o que um administrador mudou. Uma chave nunca existe no banco
   sem existir no código.
2. **Um ponto de checagem.** A checagem roda num pipeline behavior do mediator, então HTTP, bot do
   Telegram, Assistant e jobs passam todos por ela. O atributo de controller existe só para os poucos
   endpoints que não passam pelo mediator.
3. **A negação vence.** Permissões efetivas = papéis ∪ permissões diretas − negações diretas.
4. **Flag vence permissão.** Uma feature desligada fica desligada para todos, inclusive o admin
   master.
5. **Mudanças valem na hora.** O conjunto efetivo fica em cache na memória e é descartado a cada
   mudança; nunca vai dentro do JWT.
6. **Erro de digitação derruba a subida.** Ao subir, toda chave usada em `[RequiresPermission]`
   precisa existir no catálogo, ou o host não sobe.
7. **Esconder no frontend é só conforto.** Quem decide é o backend; o cliente esconde botões e rotas
   para o usuário não esbarrar num 403.

---

## 4. Tars: feature flags

`Pottmayer.Tars.FeatureFlags`

- **Catálogo.** Cada módulo contribui suas flags com um padrão e uma chave i18n de rótulo:
  ```csharp
  flags.Add("finances", defaultOn: true);
  flags.Add("finances.import", defaultOn: true, parent: "finances");
  ```
  Uma flag filha fica desligada sempre que a mãe está desligada.
- **Store.** `IFeatureFlagStore` só lê as alterações; quem grava é a aplicação (junto com a entrada
  de auditoria), que depois chama `IFeatureFlags.Invalidate()`. O Tars traz uma store em memória; o
  Pandora implementa uma no Postgres (`adm001`).
- **Uso.** `IFeatureFlags.IsEnabled(key)` no código; `[RequiresFeature(key)]` no controller (do
  `.AspNetCore`). Uma feature desligada devolve `Error.NotFound("feature.disabled", …)`.
- **Cache.** O mapa de flags é pequeno: fica todo na memória e é recarregado quando uma alteração
  muda.

## 5. Tars: autorização

`Pottmayer.Tars.Security.Authorization`

### 5.1 Catálogo

Cada módulo declara suas permissões com um builder curto. O atalho CRUD gera as quatro ações de
sempre:

```csharp
catalog.Module("finances", feature: "finances")
    .Resource("cards").Crud()                         // finances.cards.read/create/update/delete
    .Resource("imports", feature: "finances.import").Actions("run", "read")
    .Resource("transactions").Crud().Actions("approve");
```

Cada permissão carrega:
- uma chave i18n de rótulo, montada a partir da chave por convenção;
- sua **feature**: a permissão só é checada depois dessa flag;
- suas **dependências**: `create`, `update` e `delete` exigem `read`, então o painel recusa dar
  `delete` sem `read`.

### 5.2 Concessões

- **Papel**: um conjunto nomeado de padrões de permissão. Um padrão é uma chave exata ou um coringa
  (`finances.*`, `finances.cards.*`). Coringas cobrem também permissões criadas em versões futuras.
- **Papéis do usuário**: um usuário pode ter quantos papéis quiser.
- **Exceções do usuário**: `allow` ou `deny` num padrão para um usuário.
- **Papéis de sistema**: `admin` tem `*` e não pode ser editado nem excluído. Um papel é marcado como
  **padrão** e vai para toda conta nova.

### 5.3 Resolução e checagem

- `IPermissionChecker.HasAsync(userId, key)` expande os papéis e exceções do usuário contra o
  catálogo num conjunto plano de chaves, em cache por usuário. Qualquer mudança num papel, nos
  papéis de um usuário ou nas exceções dele descarta as entradas afetadas (mudou o papel → todos que
  o têm).
- `AuthorizationBehavior<TRequest, TResponse>` roda para requisições marcadas com
  `[RequiresPermission("finances.cards.delete")]`:
  1. a feature da permissão está ligada, senão `Error.NotFound("feature.disabled")`;
  2. o ator tem a chave, senão `Error.Forbidden("permission.denied")`;
  3. senão, chama o handler.

  Os dois voltam como falha de `Result`, então o `DefaultHttpErrorMapper` que já existe transforma em
  404/403, e o bot ou o Assistant podem responder com uma mensagem.
- **Quem é o ator** vem de `IAuthorizationActorResolver<TRequest>`, registrado pela aplicação. O do
  Pandora lê `Input.UserId`, que todo command de usuário já carrega (o controller preenche a partir do
  token, o bot a partir do usuário vinculado). Requisições sem `[RequiresPermission]`, como jobs de
  sistema (`RunRecurrenceGeneration`), não são checadas.
- O `.AspNetCore` traz o `[RequiresPermission]` para controllers, por um policy provider com o
  prefixo `perm:`, como o `DevicePolicyProvider` que já existe.

### 5.4 Store

`IAuthorizationStore` lê papéis, padrões dos papéis, papéis dos usuários e exceções dos usuários. O
pacote `.EntityFrameworkCore` traz as entidades e configurações com **nomes de tabela
configuráveis**, então o Pandora as mapeia para `adm002`–`adm005` e continua escrevendo as próprias
migrations `.sql`.

---

## 6. Pandora: o módulo Admin

### 6.1 Modelo de dados

| Tabela | Guarda |
|---|---|
| `adm001_feature_flag` | `key` (PK), `enabled`, `updated_by`, `updated_at`: só as flags alteradas |
| `adm002_role` | `id`, `name`, `description`, `is_system`, `is_default` |
| `adm003_role_permission` | `role_id`, `pattern` |
| `adm004_user_role` | `user_id`, `role_id` |
| `adm005_user_permission` | `user_id`, `pattern`, `effect` (`allow` / `deny`) |
| `adm006_audit_entry` | `id`, `actor_id`, `action`, `target_type`, `target_id`, `before` jsonb, `after` jsonb, `occurred_at` |

Padrões que sobraram no banco para uma chave que não existe mais são ignorados e sinalizados no
painel.

### 6.2 Catálogos

O `Abstractions` de cada módulo declara suas flags e permissões (`FinancesPermissions`,
`FinancesFeatures`) como constantes tipadas, usadas tanto no catálogo quanto no
`[RequiresPermission]`. O Admin declara as dele: `admin.users.read`, `admin.users.manage`,
`admin.roles.manage`, `admin.features.manage`, `admin.audit.read`. O acesso ao Admin também é uma
permissão, então dá para delegar sem entregar `*`.

### 6.3 Bootstrap do admin master

- `Pandora:Admin:BootstrapEmails` lista os emails que precisam ter o papel `admin`.
- Ao subir, e sempre que uma conta é ativada (`AccountActivated`), um usuário da lista recebe o papel
  se ainda não tiver. É idempotente e funciona no Docker por variável de ambiente.
- Proteções: o último dono do `admin` não pode perdê-lo nem ser desativado, e quem tem `admin`
  precisa ter MFA ligado (o login manda configurar antes).

### 6.4 Entrada na instalação atual

A primeira migration cria:
- o papel de sistema `admin` (`*`);
- um papel `member` marcado como padrão, com um coringa por módulo de usuário (`finances.*`,
  `notes.*`, `agenda.*`, …), nunca `admin.*`;
- o `member` para todo usuário que já existe.

Nada muda para ninguém até um administrador editar um papel.

### 6.5 O que os outros módulos consomem

- **O Admin consome o Identity** por uma porta no `Identity.Abstractions`: listar e buscar usuários,
  desativar/ativar, derrubar sessões. O Identity continua dono dos usuários.
- **Os módulos consomem o Admin** só pelas abstrações do Tars (`IFeatureFlags`,
  `IPermissionChecker`) e pelas próprias marcas `[RequiresPermission]`. Nenhum módulo referencia os
  projetos do Admin.
- **O Assistant** filtra as ferramentas que oferece ao modelo pelas permissões do usuário, então nunca
  propõe uma ação que seria recusada.

### 6.6 API

- `GET /identity/me` ganha `features: string[]` (chaves ligadas) e `permissions: string[]` (as chaves
  efetivas do usuário, com os coringas expandidos).
- `GET /admin/catalog`: módulos → recursos → ações, com rótulos, features e dependências.
- Usuários: lista com filtros, detalhe com **as permissões efetivas e a origem de cada uma**, adicionar
  ou remover papel em lote, copiar as permissões de outro usuário, exceções, desativar/ativar,
  derrubar sessões.
- Papéis: CRUD, padrões, quem tem.
- Features: lista com estado e padrão, liga/desliga.
- Auditoria: lista paginada, filtro por ator, alvo e período.

Toda escrita vai para o `adm006` com o estado de antes e de depois.

### 6.7 Frontend

- `usePermission(key)`, `useFeature(key)` e `<Can permission="…">` leem do `/me`. O menu mostra um
  módulo quando a flag dele está ligada e o usuário tem alguma permissão nele; as rotas fazem o mesmo
  e mostram uma página de 403 no caso contrário.
- **Tela de papéis**: uma matriz com módulo → recurso nas linhas e ações nas colunas. As linhas de
  módulo e de recurso têm um checkbox de três estados que marca tudo abaixo. Busca por nome,
  permissões de uma feature desligada em cinza, quantos usuários têm o papel, aviso quando falta uma
  dependência.
- **Tela de usuários**: a lista padrão (ocupa a altura da janela, painel agrupado), seleção múltipla
  com *adicionar papel* / *remover papel* em lote. A gaveta de detalhe lista as permissões efetivas
  com a origem ("via Finanças – leitura", "exceção direta") e edita as exceções ali mesmo.
- **Tela de features**: a árvore de flags com interruptores; desligar uma mãe mostra o que vai junto.
- **Tela de auditoria**: linha do tempo só de leitura.

---

## 7. Entrega

| Etapa | Repositório | Conteúdo | Pronto quando |
|---|---|---|---|
| A1 | tars | `FeatureFlags` (catálogo, stores, `IFeatureFlags`, `[RequiresFeature]`) | testes verdes, docs em `docs/feature-flags/` |
| A2 | tars | `Security.Authorization` (builder do catálogo, resolução com coringas e negação, cache, behavior, atributo ASP.NET, store EF) | testes verdes, docs em `docs/security/` |
| A3 | pandora | Módulo Admin, `adm001`–`adm006`, catálogos de todos os módulos, `[RequiresPermission]` em todo command e query de usuário, resolvedor de ator do Pandora, bootstrap, `/me` com features e permissões, filtro de ferramentas do Assistant | testes atuais verdes, testes novos para 403/404 e bootstrap; nada muda para o usuário atual |
| A4 | pandora | Hooks do frontend, guardas do menu e das rotas, telas de papéis, usuários, features e auditoria | conferido no navegador pelo user |

## 8. Decisões

| Decisão | Escolha |
|---|---|
| Modelo de permissão | Papéis + permitir/negar por usuário, a negação vence |
| Onde a checagem roda | Pipeline behavior do mediator, atributo de controller só para o resto |
| Store das flags | Banco, liga/desliga no painel |
| Bootstrap do admin master | `Pandora:Admin:BootstrapEmails`, aplicado ao subir e na ativação |
| Atualização | Consulta no banco com cache em memória, descartado a cada mudança; nada no JWT |
| Resposta quando desligado / negado | 404 `feature.disabled` / 403 `permission.denied` |

Fora por enquanto: "entrar como" outro usuário, números de uso por módulo, página de saúde do
sistema, permissões por tenant, planos ou cobrança.
