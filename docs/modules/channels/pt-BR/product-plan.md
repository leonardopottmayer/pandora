# Módulo Channels — Roadmap (trabalho restante)

> **Status:** as fases **C1–C5** estão implementadas. Este arquivo agora
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
estão todos documentados nos arquivos linkados acima e estão **construídos**. A fase C5 também está
completa agora.

---

## Fase C5 — Operações *(feito)*

- **Quiet hours — construído.** Uma janela diária global de "não perturbe"
  (`chn007_user_notification_setting`), `suppress | deliver-anyway` (`defer_to_end` descartado —
  segurar até de manhã é agendamento, que não vive aqui, C1). Avaliada no fuso IANA do próprio usuário
  (resolvido das preferências do Identity no momento da entrega) e aplicada no
  `NotifyUserRequestedHandler` antes do fan-out. Chegou como ajuste **global por usuário** em vez de
  colunas na `chn005`, porque um único "não perturbe" numa tabela por categoria teria virado uma linha
  por categoria. Notificações de segurança passam por cima.
- **Métricas — construído.** Um meter `ChannelsMetrics` (`Pottmayer.Pandora.Modules.Channels`) expõe
  profundidade de fila, duração de dispatch, contagem de despachos por canal/desfecho e updates de
  entrada descartados, assinado por um wildcard `AddMeter` `Pottmayer.Pandora.*` na fiação de
  observabilidade compartilhada e exportado via OTLP (o pipeline OpenTelemetry do Host, que chegou
  desde que isto foi planejado).

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
