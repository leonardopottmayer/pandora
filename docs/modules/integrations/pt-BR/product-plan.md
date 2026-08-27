# Módulo Integrations — Roadmap (trabalho restante)

> **Status:** a fase **I1 (Core)** está implementada. Este arquivo agora rastreia só o que **ainda não
> foi construído**. Para o que existe, ver os docs do módulo: [README](README.md) ·
> [Visão geral](overview.md) · [Arquitetura](architecture.md) · [Modelo de dados](data-model.md) ·
> [OAuth e Credenciais](oauth-and-credentials.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Agenda](../../agenda/pt-BR/product-plan.md) ·
> [Channels](../../channels/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md)

---

## Recapitulação de design (já decidido)

O limite, os princípios (I1–I5), o modelo de domínio, a semântica de refresh, a encriptação e o fluxo
de autorização estão documentados nos arquivos linkados acima e estão **construídos**. O que resta é
resiliência, gestão de chaves de API e mais provedores.

---

## Fase I2 — Resiliência *(próxima)*

- **Reação de Channels à revogação.** Um subscriber de `ExternalAccountRevoked` (já publicado) que
  manda uma mensagem no Telegram dizendo ao usuário para reconectar, mais o template. Idem para
  `ExternalAccountDisconnected` onde um consumidor precise reagir.
- **Log de eventos `int003`** — append-only de conexões/refreshes/falhas/revogações; a única forma de
  responder "por que o sync parou três dias atrás". Expor a saúde da conexão em configurações.
- **Pronto quando:** revogar o acesso na página da conta Google produz uma mensagem no Telegram dizendo
  para reconectar, e o sync para de forma limpa em vez de tentar para sempre.

## Fase I3 — Chaves de API *(pré-requisito para a fase A5 do [Assistant](../../assistant/pt-BR/product-plan.md))*

- Endpoints de registrar / rotacionar / remover para contas `auth_kind = api_key` (o caminho de
  leitura, `GetApiKeyAsync`, já existe).
- `openai` e `gemini` no catálogo de provedores, com **nenhum fluxo de autorização** — só um formulário
  com a chave e um teste de alcance.
- **Pronto quando:** o Assistant consegue chamar a OpenAI com uma chave que nunca viu em texto puro, e o
  mesmo cofre guarda o refresh token do Google.

## Fase I4 — Mais provedores *(guiado por demanda, não agendado)*

- Microsoft (Outlook Calendar / To Do), CalDAV (genérico; cobre Apple/Fastmail/Nextcloud).

---

## Perguntas em aberto

1. **Gate in-process vs. advisory lock.** A implementação serializa o refresh com um gate in-process
   (monólito de processo único). Reavaliar só se o host escalar horizontalmente.
2. **Multi-conta por provedor.** A constraint única `(user_id, provider, provider_account_id)` já
   modela duas contas Google. Se as bindings de um consumidor podem cruzá-las é decisão do consumidor,
   não deste módulo.
