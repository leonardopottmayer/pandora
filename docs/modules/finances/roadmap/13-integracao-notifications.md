# Fase 13 — Integração com Notifications

> Pré-requisitos: fases 05, 08 e 09 (produtores dos eventos).
> Referência: [finances-module.md](../finances-module.md) §11.

## Objetivo

Fechar o ciclo com o usuário: avisos de fatura e de itens aguardando revisão, via módulo Notifications existente.

## Escopo

### Inclui
- Integration events em `Finances.Contracts` (publicados pelos casos de uso/jobs já existentes):
  - `StatementClosed` (cartão, valor, vencimento);
  - `StatementDueSoon` (X dias antes — configurável via Options, default 3; emitido pelo `StatementLifecycleService`);
  - `StatementOverdue`;
  - `ImportCompleted` (n sugestões aguardando revisão);
  - `PendingTransactionsGenerated` (resumo das pendentes geradas pelo job de recorrência — agregado por execução, não um evento por pendente).
- Subscribers no módulo Notifications + templates de e-mail correspondentes (locale do usuário, padrão dos templates existentes).
- Idempotência: cada evento de fatura emitido no máximo uma vez por transição (re-rodar o job não re-notifica); `correlation_id` propagado do evento de domínio até a notificação.
- Preferências de notificação por tipo (opt-out) — avaliar se entra aqui ou aproveita mecanismo existente do Notifications; decidir no início da fase.

### Não inclui
- Push/outros canais (segue capacidade atual do Notifications); resumos periódicos tipo "fechamento do mês" (futuro).

## Critérios de aceite
1. Fechamento de fatura dispara exatamente um e-mail com valor e vencimento corretos; reprocessamento do job não duplica.
2. Lembrete de vencimento sai X dias antes apenas para faturas não quitadas; fatura paga antes do lembrete não notifica.
3. Importação concluída com ≥1 sugestão notifica com a contagem; com zero sugestões, não notifica.
4. Eventos carregam o mínimo necessário (ids + dados de exibição), sem acoplar Notifications ao schema de Finances.
