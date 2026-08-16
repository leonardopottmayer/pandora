# Módulo Integrations — Plano de Produto

> **Status:** Plano. Nada neste documento está implementado ainda.
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Agenda](../../agenda/pt-BR/product-plan.md) ·
> [Channels](../../channels/pt-BR/product-plan.md) ·
> [Assistant](../../assistant/pt-BR/product-plan.md)

---

## 1. O que o módulo faz

**Integrations** é dono das credenciais que o Pandora usa **em nome do usuário** para chamar serviços
de terceiros: a dança do OAuth, os tokens, sua renovação e sua revogação — e as chaves de API que o
próprio usuário fornece. Nada além disso.

É deliberadamente o menor módulo do Pandora. Ele responde exatamente uma pergunta para o resto do
sistema:

> "Me dê uma credencial válida para o usuário *U* no provedor *P*."

A [Agenda](../../agenda/pt-BR/product-plan.md) é seu primeiro e, no lançamento, único consumidor — ela
precisa de um token do Google para sincronizar calendários e tarefas. Mas credenciais de serviços
externos não são assunto de calendário, e tanto o Finances (open finance) quanto o Assistant (chaves de
LLM hospedado) são segundos consumidores plausíveis. Separar o cofre de credenciais desde o primeiro dia
significa que nenhum dos dois vai precisar enfiar a mão no schema da Agenda depois.

### O que ele não é

Ele **não** é um motor de sincronização. Não sabe o que é um calendário, um evento ou uma tarefa. Os
cursores de sync, os mapeamentos de entidade, a resolução de conflito e os adaptadores de provedor vivem
todos na Agenda, que é o módulo que entende o dado sendo sincronizado. O Integrations entrega tokens; a
Agenda os usa.

Ele também **não** é dono de *canal*. O chat id do Telegram é um **endereço** onde o Pandora alcança
o usuário, e o bot token é uma credencial de **deployment**, não do usuário — nenhum dos dois é
"credencial do usuário para o Pandora chamar um terceiro em nome dele". Os dois vivem no
[Channels](../../channels/pt-BR/product-plan.md). A fronteira, em uma linha:

> **Integrations:** o Pandora chama um terceiro *como* o usuário. **Channels:** o Pandora fala *com*
> o usuário.

Na interface isso não aparece: a tela de configurações tem uma seção **Conexões** só, composta por
duas seções de backend. Unidade de UI não é unidade de módulo.

---

## 2. Nomenclatura e coordenadas

| Item | Valor |
|---|---|
| Projetos backend | `Pottmayer.Pandora.Modules.Integrations.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `integrations` |
| Prefixo de tabela | `intXXX_`, PK `uuid_generate_v7()` |
| Base da API | `/api/v{version}/integrations` |
| Frontend | `client-web/src/modules/integrations` (ou uma seção de configurações — ver §7) |
| Migrations | `migrations/migrations/integrations/` |

---

## 3. Princípios

1. **Tokens nunca saem do módulo em forma armazenável.** Consumidores recebem um access token de vida
   curta por uma chamada de porta, não por um repositório. Não existe `GetRefreshToken`. *(I1)*
2. **A renovação é invisível.** `GetAccessTokenAsync` devolve algo válido ou falha. O chamador nunca
   implementa refresh, e existe exatamente um lugar que pode competir por ele. *(I2)*
3. **Criptografado em repouso, sempre.** Refresh tokens são a joia da coroa do sistema inteiro — um
   roubado lê o calendário e a caixa de entrada reais do usuário para sempre. São criptografados com uma
   chave que não está no banco. *(I3)*
4. **Provedores são configuração mais um adaptador.** Adicionar Microsoft é um client-id, uma entrada de
   metadados e uma implementação de `IOAuthProvider`. O domínio não muda. *(I4)*
5. **Uma conexão revogada degrada, nunca quebra.** Um consumidor pedindo token de uma conta revogada
   recebe uma falha tipada e uma notificação é enviada ao usuário, não um stack trace num job de
   background. *(I5)*

---

## 4. Modelo de domínio

### 4.1 Catálogo de schema

**`int001_external_account`** — uma conta de terceiro conectada.

| Coluna | Observações |
|---|---|
| `user_id` | Dono. |
| `provider` | `google` hoje; `microsoft`, `apple`, `caldav`, `openai`, `gemini` depois. |
| `auth_kind` | `oauth` \| `api_key`. Decide quais colunas são obrigatórias e se há fluxo de autorização. |
| `provider_account_id` | O id estável do subject no provedor. Único junto com `(user_id, provider)`. Para `api_key`, um discriminador escolhido pelo usuário (rótulo da chave). |
| `display_name` | O email/handle da conta, mostrado nas configurações. |
| `scopes` | Escopos concedidos, como armazenados — usados para detectar que uma funcionalidade nova precisa de novo consentimento. |
| `access_token_enc`, `access_token_expires_at` | Criptografado; vida curta. |
| `refresh_token_enc` | Criptografado. Nulo quando o provedor não emite. |
| `status` | `connected` \| `expired` \| `revoked` \| `needs_consent`. |
| `connected_at`, `last_refreshed_at`, `last_error` | |

**`int002_oauth_state`** — a requisição de autorização em voo.

| Coluna | Observações |
|---|---|
| `user_id`, `provider`, `state` | `state` é o token CSRF, único, de uso único. |
| `code_verifier_enc` | PKCE. Criptografado, porque é uma credencial pela duração do fluxo. |
| `redirect_after` | Para onde devolver o browser dentro da SPA. |
| `expires_at`, `consumed_at` | TTL de 10 minutos, uso único. |

**`int003_integration_event_log`** *(opcional, fase I3)* — registro append-only de conexões,
renovações, falhas e revogações. Pequeno, barato, e a única forma de responder "por que o sync parou há
três dias".

### 4.2 Portas

Publicadas de `Integrations.Abstractions`, a única coisa que outros módulos referenciam:

```csharp
public interface IExternalCredentialProvider
{
    // auth_kind = oauth — renova de forma invisível
    Task<Result<ExternalAccessToken>> GetAccessTokenAsync(
        Guid userId, string provider, CancellationToken ct = default);

    // auth_kind = api_key — decifra e devolve; não expira, não renova
    Task<Result<string>> GetApiKeyAsync(
        Guid userId, string provider, CancellationToken ct = default);
}

public interface IExternalAccountReader
{
    Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<ExternalAccountSummary?> GetAsync(Guid externalAccountId, CancellationToken ct = default);
}
```

`ExternalAccessToken` carrega a string do token, sua expiração e os escopos concedidos. É um valor
transitório — nunca persistido pelo consumidor, nunca logado.

Interno ao módulo:

```csharp
public interface IOAuthProvider          // um por provedor
{
    string Name { get; }
    Uri BuildAuthorizationUrl(OAuthAuthorizationRequest request);
    Task<OAuthTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);
    Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
}

```

Vindo do Tars (`Pottmayer.Tars.Security.DataProtection`):

```csharp
public interface ISecretProtector          // AES-GCM sobre uma chave de configuração/secret store
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
```

### 4.3 Semântica de renovação

`GetAccessTokenAsync` é o caminho quente — o job de sync da Agenda chama a cada poucos minutos:

1. Se o access token em cache tem mais de 60 segundos de margem, devolve.
2. Senão, toma um lock consultivo por conta (`pg_advisory_xact_lock` sobre o id da conta), relê, e só
   renova se ainda for necessário. Dois jobs de sync concorrentes não podem queimar duas renovações;
   alguns provedores invalidam o refresh token anterior na rotação.
3. Persiste o novo par, criptografado; atualiza `last_refreshed_at`.
4. Em `invalid_grant`, marca a conta como `revoked`, publica `ExternalAccountRevoked` e devolve falha
   tipada.

`ExternalAccountRevoked` é consumido pelo Channels, que avisa o usuário que a conexão com o Google
precisa ser refeita — o único caso em que uma falha de background precisa chegar num humano.

### 4.4 Gestão de chave

`ISecretProtector` lê uma chave de 256 bits da configuração (variável de ambiente no Docker, secret
montado no homelab), nunca do banco. O ciphertext é guardado com prefixo de versão de chave, então
rotacionar é uma recriptografia em background e não um evento de "reconecte tudo". A chave viver fora do
banco é o ponto inteiro: um dump do banco sozinho não pode render credenciais do Google funcionando.

---

## 5. Fluxo de autorização

Authorization code flow server-side com PKCE. A SPA nunca vê um token.

```
1. SPA        → POST /integrations/google/connect
                ← { authorizationUrl }              (state + verifier persistidos)
2. Browser    → tela de consentimento do provedor
3. Provedor   → GET /integrations/google/callback?code=&state=
4. Backend    → valida e consome o state, troca o code, upsert da conta (criptografada)
                ← 302 para o redirect_after na SPA
5. SPA        → GET /integrations/accounts        ← mostra a conexão viva
```

O callback é o único endpoint anônimo; ele se autentica pelo `state` de uso único que ele mesmo emitiu.
Escopos são pedidos de forma **incremental** — conectar para calendário pede só escopos de calendário;
habilitar sync de tarefas depois dispara um novo consentimento que amplia e atualiza `scopes`.

### Especificidades do Google
- `access_type=offline` e `prompt=consent` na primeira conexão, para de fato receber um refresh token.
- Escopos: `calendar` e `calendar.events` para a fase 5 da Agenda; `tasks` adicionado na fase 6.
- Um projeto no Google Cloud com a tela de consentimento OAuth configurada é pré-requisito de
  deployment, documentado em `docs/deployment/`.

---

## 6. Superfície de API

```
GET    /integrations/providers                 → catálogo: nome, descrição, escopos, conectado?
POST   /integrations/{provider}/connect        → { authorizationUrl }
GET    /integrations/{provider}/callback       → anônimo; consome o state, redireciona para a SPA
GET    /integrations/accounts                  → contas conectadas, status, escopos, último erro
POST   /integrations/accounts/{id}/reconnect   → novo consentimento para escopos ampliados
DELETE /integrations/accounts/{id}             → revoga no provedor, depois apaga localmente
```

Apagar uma conta publica `ExternalAccountDisconnected`. A Agenda assina e desabilita os vínculos que a
usavam, deixando os dados sincronizados no lugar — desconectar o Google não pode apagar os eventos do
usuário.

---

## 7. Frontend

Este módulo não tem tela própria. Ele contribui uma seção **Contas conectadas** na área de
configurações: cards de provedor com status, escopos, conectar/desconectar, e o aviso de "reconexão
necessária" quando `status = revoked`. A tela de configurações da Agenda aponta para cá.

Se a seção crescer além de um punhado de provedores, ela vira `client-web/src/modules/integrations`; até
lá mora junto com o resto das configurações, e a pegada de frontend do módulo é um hook e um componente.

---

## 8. Roadmap

### Fase I1 — Núcleo *(pré-requisito da fase 5 da Agenda)*
- Sete projetos, schema `integrations`, `int001`/`int002`.
- `ISecretProtector` (AES-GCM, chave versionada), `IOAuthProvider`, implementação Google.
- Endpoints de connect/callback/list/disconnect; `IExternalCredentialProvider` com renovação sob lock.
- Seção de UI nas configurações.
- **Pronto quando:** o job de sync da Agenda obtém um token válido do Google atravessando a expiração
  de um access token, sem que nenhum código da Agenda saiba o que é um refresh token.

### Fase I2 — Resiliência
- Contratos `ExternalAccountRevoked` / `ExternalAccountDisconnected` e templates no Channels.
- Ampliação incremental de escopos e o caminho de novo consentimento.
- `int003` log de eventos; saúde da conexão visível nas configurações.
- **Pronto quando:** revogar o acesso na página da conta Google produz uma mensagem no Telegram pedindo
  reconexão, e o sync para de forma limpa em vez de tentar para sempre.

### Fase I3 — Chaves de API *(pré-requisito da fase A5 do Assistant)*
- `auth_kind = api_key` na `int001`; endpoints de cadastro/rotação/remoção; `GetApiKeyAsync`.
- Provedores `openai` e `gemini` no catálogo, sem fluxo de autorização — só um formulário com a chave
  e um teste de alcance.
- **Pronto quando:** o [Assistant](../../assistant/pt-BR/product-plan.md) consegue chamar a OpenAI com
  uma chave que ele nunca viu em texto claro, e o mesmo cofre guarda o refresh token do Google.

### Fase I4 — Mais provedores *(por demanda, sem data)*
- Microsoft (Outlook Calendar / To Do), CalDAV (genérico, cobre Apple/Fastmail/Nextcloud).

---

## 9. Questões em aberto

1. ~~**O módulo guarda segredos que não são OAuth?**~~ **Decidido: sim.** `int001` ganha
   `auth_kind = oauth | api_key`, e uma chave da OpenAI é uma linha como outra qualquer — mesmo
   cofre, mesma criptografia, sem o fluxo de autorização. Fecha a
   [questão 1 do Assistant](../../assistant/pt-BR/product-plan.md#9-questões-em-aberto).
2. ~~**Onde vive o `ISecretProtector`.**~~ **Decidido:** no Tars,
   `Pottmayer.Tars.Security.DataProtection`. Deixou de ser código de um consumidor só — este módulo
   usa para OAuth e para chaves de API, e o roberto tem o mesmo problema.
3. **Múltiplas contas por provedor.** A constraint única é `(user_id, provider, provider_account_id)`,
   então duas contas Google já estão modeladas. Se os vínculos da Agenda podem atravessar as duas é
   decisão da Agenda, não deste módulo.
