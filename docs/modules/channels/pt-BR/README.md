# Módulo Channels

> O dono da conversa com o usuário — nas duas direções, em todo canal — dentro do monólito modular do Pandora.
> **Idioma:** o inglês é a documentação primária. 🇺🇸 [English version](../README.md).

O módulo **Channels** (antes `Notifications`) é dono de como o Pandora **fala com o usuário**: uma
fila de saída durável sobre **e-mail** e **Telegram**, política de entrega por usuário, e o tráfego de
entrada — vínculo de conta, callbacks de botão inline, mensagens de texto e notas de voz — roteado de
volta para o módulo dono.

O limite que o define, em uma linha:

> **Channels:** o Pandora fala *com* o usuário. **[Integrations](../../integrations/README.md):** o
> Pandora chama um terceiro *como* o usuário.

Duas regras que guiam tudo: **Channels envia agora, não agenda** (quem quer entrega às 14:00 chama às
14:00) e **a entrada é classificada estruturalmente, nunca semanticamente** — o módulo resolve um id
numa tabela e lê a coluna que o módulo dono escreveu; nunca interpreta o que uma ação *significa*.

---

## Como esta documentação está organizada

Comece pela **Visão geral** para o limite e o vocabulário, depois leia o tópico que precisar.

| # | Documento | O que cobre |
|---|---|---|
| 1 | [Visão geral](overview.md) | O que o módulo faz, o limite Channels/Integrations, princípios, linguagem ubíqua, escopo |
| 2 | [Arquitetura](architecture.md) | Organização de projetos, a costura Delivery/Ingress/Addressing, blocos de domínio, decisões, o bloco Telegram do Tars |
| 3 | [Modelo de dados](data-model.md) | Catálogo de schema (`chn001`–`chn006`): colunas, constraints, índices |
| 4 | [Saída e Templates](outbound-and-templates.md) | Enfileiramento, fan-out, renderização por canal, árvore de templates, botões, o dispatcher e retry |
| 5 | [Entrada e Vínculo](inbound-and-linking.md) | O handshake do Telegram, long polling, triagem, interações, mídia, roteamento de volta aos donos |
| 6 | [Referência de API](api-reference.md) | Todos os endpoints sob `/api/v{n}/channels` |
| 7 | [Status de implementação](implementation-status.md) | O que está pronto vs. planejado |

O roadmap adiante (resto da fase C5 e além) fica em [product-plan.md](product-plan.md).

---

## Fatos rápidos

- **Backend:** `Pottmayer.Pandora.Modules.Channels.*` (.NET 10, DDD, comandos/queries no estilo CQRS).
- **Schema:** schema PostgreSQL `channels`, tabelas com prefixo `chnXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** uma seção **Notificações** dentro de configurações (canais, envio de teste, preferências, histórico de entrega).
- **Base da API:** `/api/v{version}/channels`, autenticada e escopo do usuário do token.
- **Migrations:** `migrations/migrations/channels/`.
- **Transportes:** e-mail via `Pottmayer.Tars.Communication.Email.MailKit`; Telegram via
  `Pottmayer.Tars.Communication.Telegram`.
