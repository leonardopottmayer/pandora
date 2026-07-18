# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído no código versus o que está projetado mas ainda não implementado.
Use para distinguir "documentado porque existe" de "documentado como plano".

---

## Implementado

| Área | Notas |
|---|---|
| **Scaffold do módulo + auditoria** | Todos os projetos por camada, schema `finances`, trilha de auditoria `fin016` ligada a toda mutação. |
| **Categorias** | Categorias do sistema (seed, árvore de 2 níveis) + CRUD de categorias do usuário (`fin002`, `fin003`). |
| **Contas** | CRUD, tipos, moeda imutável, arquivar/desarquivar, saldo inicial (`fin001`). |
| **Ledger** | Lançamentos, 11 kinds incl. `statement-writeoff`, sinais, máquina de status, saldo derivado, transferências (`fin008`). |
| **Cartões e faturas** | CRUD de cartão, ciclo da fatura, resolver, pagar, **quitar (write-off)**, fechar, **reabrir**, limite disponível (`fin006`, `fin007`). |
| **Parcelamento** | Planos manuais, divisão de centavos, cancelar parcela/plano; planos inferidos de importação + projeções (`fin009`). |
| **Tags** | CRUD de tags + vínculos polimórficos (`fin004`, `fin005`). |
| **Recorrências e inbox** | Templates de recorrência + motor de regras, inbox de staging, aprovar/rejeitar/vincular/transferir-do-pendente, job de geração (`fin010`, `fin011`). |
| **Importação** | OFX **e** CSV, seed de layouts de banco + auto-detecção, dedup/conciliação de três níveis, detecção de parcelas & projeção, data de corte, retry (`fin012`–`fin014`). |
| **Reversibilidade** | Cancelar, desfazer, estornar (todos os casos), proteções de exclusão em conta/cartão. |
| **Leituras de auditoria** | Timeline `/audit` por entidade ou correlation id. |
| **Frontend** | Módulo React (`client-web/src/modules/finances`) cobrindo contas, cartões, faturas, lançamentos, transferências, categorias, tags, recorrências, inbox, importações, auditoria. |

### Adições além do design original

A implementação evoluiu além da primeira proposta de design. Adições notáveis:

- **Kind `statement-writeoff`** + `SettleStatement` — quitação sem caixa de faturas pré-Pandora no
  onboarding.
- **`ReopenStatement`** — reabrir uma fatura fechada para novas compras.
- **Data de corte de importação** — pular linhas antes de uma data para não inundar o inbox no go-live.
- **Retry de importação** + armazenamento dos bytes brutos para reprocessar.
- **Auto-detecção de layout** (`ILayoutDetector`) para arquivos enviados.
- **Importação CSV** com layouts por banco (originalmente uma fase posterior).
- **Transferência do pendente** e **vincular a existente** no inbox.
- **Flag `auto_generate`** nas recorrências (opt-out da geração automática).
- `import_file.file_hash` é **informativo, não único** — reimportação deliberada é permitida.

## Ainda não implementado (projetado / planejado)

| Área | Status |
|---|---|
| **Regras de categorização** (`fin015`) | Auto-categorizar sugestões de importação ("descrição contém UBER → Transporte"). Tabela e casos de uso não criados. |
| **Relatórios** | Endpoints de fluxo de caixa, por categoria, histórico de saldo e agenda de vencimentos não implementados — só existe a timeline de auditoria. |
| **Eventos de integração com Notifications** | O projeto `Contracts` está vazio; `StatementClosed`/`StatementDueSoon`/`StatementOverdue`/`ImportCompleted`/`PendingTransactionsGenerated` não são publicados, e não há subscribers/templates no Notifications. |
| **Layouts de importação criados pelo usuário** | Só há seed de layouts de sistema; layouts do usuário + endpoint de preview estão reservados para uma fase futura. |
| **Split de transação, orçamentos, metas, anexos, snapshots de saldo, consolidação multi-moeda, open finance, household multiusuário** | Futuro — o modelo deixa espaço sem reescrever o core. |

## Pontos em aberto conhecidos

1. **Calibração da heurística de duplicata** — a janela de ±2 dias e a tolerância de valor para
   duplicatas suspeitas são chutes iniciais; calibrar com extratos reais (possivelmente torná-los
   configuráveis por usuário/layout).
2. **Retenção do `file_content`** — os bytes brutos ficam no banco (`bytea`); revisar mover para object
   storage ou expurgar após N meses.
3. **Auto-post de recorrência em cartão** — se postar direto na fatura sem revisão é desejável, ou se o
   cartão sempre deve passar por staging.
4. **Pagamento de fatura multi-moeda** — a v1 registra o pagamento na moeda da conta + `fx_rate`;
   revisar com a consolidação multi-moeda.
5. **Backfill de parcelas passadas** — quando um plano é criado a partir da parcela N via importação,
   se gerar opcionalmente as parcelas 1..N−1 (default: não).
