# Módulo Channels — Roadmap (trabalho restante)

> **Status:** as fases **C1–C4** e a maior parte da **C5** estão implementadas. Este arquivo agora
> rastreia só o que **ainda não foi construído**. Para o que existe, ver os docs do módulo:
> [README](README.md) · [Visão geral](overview.md) · [Arquitetura](architecture.md) ·
> [Modelo de dados](data-model.md) · [Saída e Templates](outbound-and-templates.md) ·
> [Entrada e Vínculo](inbound-and-linking.md) · [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Planos relacionados: [Agenda](../../agenda/pt-BR/product-plan.md) ·
> [Integrations](../../integrations/pt-BR/product-plan.md) · [Assistant](../../assistant/pt-BR/product-plan.md) ·
> [Mensageria](../../../architecture/pt-BR/messaging.md)

---

## Recapitulação de design (já decidido)

O limite (Channels fala *com* o usuário), os princípios (C1–C6), a costura interna Delivery/Ingress/
Addressing, o modelo de templates de dois caminhos, o fan-out, a triagem de entrada e o roteamento
estão todos documentados nos arquivos linkados acima e estão **construídos**. O que resta é a cauda de
operações da fase C5.

---

## Fase C5 — Operações (restante)

O módulo é totalmente utilizável sem isto; chega aos poucos.

- **Quiet hours.** `chn005` ganha `quiet_hours_start` / `quiet_hours_end` (no fuso do usuário) e um
  `quiet_hours_behaviour` de `suppress | deliver_anyway` — `defer_to_end` é descartado, porque segurar
  uma entrega até de manhã é agendamento, e agendamento não vive aqui (C1). **Desbloqueado** — o fuso
  IANA do usuário já está disponível nas preferências do Identity — mas ainda não construído.
- **Métricas.** Profundidade de fila, latência de dispatch, taxa de falha por canal, updates
  descartados. Depende da fiação de OpenTelemetry no Host — uma tarefa transversal, não só do Channels.

## Talvez depois *(não planejado)*

- **Driver de webhook.** Long polling cobre o ingress em todo lugar, inclusive atrás de NAT, então um
  webhook só ganha lugar quando o homelab for exposto sobre HTTPS público. O cliente Tars já o suporta
  (`SetWebhookAsync`/`DeleteWebhookAsync`); o controller entregaria os updates à mesma triagem que o
  driver de long-polling usa.
- **Retry manual de uma linha morta.** Reenfileirar uma notificação morta pela UI. Não vale a superfície
  enquanto dead-letters são raras e já inspecionáveis no log.

## Follow-ups relacionados (outros módulos)

- **Categorias do Finances.** Seus eventos de fatura/import estão documentados como planejados mas não
  publicados. Quando forem, ganham categorias de entrega de graça — um follow-up pequeno no Finances.

---

## Perguntas em aberto

1. **Categorias como registry tipado ou string.** Hoje `Category` é uma string no contrato. Um registry
   central daria validação no startup ao custo de mais um lugar para tocar quando um módulo nasce.
   Tendência: string até doer.
2. **Um endereço por canal.** Dois chats do Telegram (pessoal + um grupo) estão fora de escopo; a
   constraint única faz disso uma mudança futura deliberada.
