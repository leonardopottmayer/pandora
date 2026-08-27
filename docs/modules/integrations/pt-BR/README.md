# Módulo Integrations

> O cofre de credenciais com que o resto do Pandora chama serviços de terceiros, dentro do monólito modular.
> **Idioma:** o inglês é a documentação primária. 🇺🇸 [English version](../README.md).

O módulo **Integrations** é dono das credenciais que o Pandora usa **em nome do usuário** para chamar
serviços de terceiros: a dança OAuth, os tokens, o refresh transparente e a revogação — além de
chaves de API fornecidas pelo próprio usuário. Nada além disso.

É, de propósito, o menor módulo do Pandora. Ele responde exatamente uma pergunta para o resto do
sistema:

> "Me dê uma credencial válida do usuário *U* no provedor *P*."

A regra que guia tudo: **tokens nunca saem do módulo em forma armazenável.** Consumidores pedem um
access token de curta duração (ou uma chave de API) por uma chamada de porta — não existe
`GetRefreshToken`, e o refresh é invisível para quem chama. Refresh tokens são encriptados em repouso
com uma chave que vive fora do banco.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o limite do módulo e o vocabulário, depois leia o tópico que precisar.
Cada arquivo carrega o *contexto de negócio* (o que significa e por quê) e as *regras técnicas*
(agregados, schema, portas, endpoints).

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | O que o módulo faz, o limite Integrations/Channels, princípios, linguagem ubíqua, escopo |
| 2 | [Arquitetura](architecture.md) | Organização de projetos, blocos de domínio, portas, decisões de design |
| 3 | [Modelo de dados](data-model.md) | Catálogo de schema (`int001`, `int002`): colunas, constraints, índices |
| 4 | [OAuth e Credenciais](oauth-and-credentials.md) | Fluxo authorization-code + PKCE, refresh transparente, encriptação, chaves de API |
| 5 | [Referência de API](api-reference.md) | Todos os endpoints sob `/api/v{n}/integrations` |
| 6 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |

O roadmap adiante (fases ainda não implementadas) fica em [product-plan.md](product-plan.md).

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Integrations.*` (.NET 10, DDD, comandos/queries no estilo CQRS).
- **Schema:** schema PostgreSQL `integrations`, tabelas com prefixo `intXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** uma seção **Contas conectadas** dentro de configurações — `client-web/src/modules/integrations`.
- **Base da API:** `/api/v{version}/integrations`, autenticada (o callback é o único endpoint anônimo).
- **Migrations:** `migrations/migrations/integrations/`.
- **Encriptação:** `Pottmayer.Tars.Security.DataProtection` (`ISecretProtector`, AES-GCM, chave fora do banco).
