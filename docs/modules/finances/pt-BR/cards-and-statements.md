# Cartões e faturas

[← Voltar ao índice](README.md) · Aggregates: `Card`, `CardStatement` · Tabelas: `fin006_card`, `fin007_card_statement` · API: `/cards`, `/statements`

---

## Contexto de negócio

Um **cartão de crédito** é cobrado por **faturas** mensais, não postando direto em uma conta (decisão
de design D2). Uma compra cai na fatura cujo ciclo a contém; só **pagar** (ou quitar) a fatura tira
dinheiro de uma conta real. Isso bate com o mental brasileiro de *fatura → fechamento → vencimento →
pagamento*.

Cartão de débito **não** é modelado — um débito é lançamento direto na conta.

## Cartão

- **Config:** nome (único por usuário), bandeira, últimos quatro dígitos, `account_id` (conta a que o
  cartão pertence, **obrigatória**; o `bank_code` dela roteia a importação de faturas ao layout certo
  — ver [Importação](imports.md#roteamento-por-banco-do-destino)), `credit_limit` (`>= 0`, opcional),
  `closing_day`, `due_day`, moeda.
- **`closing_day` / `due_day` ∈ 1..28** — imposto por CHECK para evitar ambiguidade com o tamanho do mês.
- **Moeda fixa** na criação (imutável, como as contas).
- **Arquivamento** funciona como nas contas: cartão arquivado some, rejeita mutações de negócio,
  mantém histórico, é reversível via desarquivar.

### Tela de cartões

A lista segue a tela de Contas: cabeçalho e ação Novo cartão acima de um painel com borda. As linhas
reúnem nome, bandeira e final; o ciclo aparece abaixo do nome em telas pequenas e o limite cadastrado
fica alinhado à direita. Clicar na linha abre o detalhe; o menu de reticências identificado pelo nome
oferece Editar, Arquivar/Desarquivar e Excluir sem navegar. Apenas arquivados recebem tag de status.
Arquivar e excluir pedem confirmação com o nome do cartão.

Mostrar arquivados continua acima da lista. No desktop, a tabela rola dentro da altura disponível,
com cabeçalho fixo e contagem no rodapé; no celular, a página rola sem transbordamento horizontal.
A lista vazia explica o que falta. Esta tela não calcula limite disponível ou soma de limites.

O modal coloca nome/conta/moeda e dias do ciclo primeiro, com campos em pares a partir de `sm`.
Bandeira, final e limite ficam nas opções adicionais, abertas ao editar quando há algum desses
valores. O limite mostra a moeda escolhida e a moeda continua bloqueada na edição. Formulários
longos rolam dentro do modal. Valores padrão, validações e contratos das requisições permanecem
iguais.

### Tela de detalhe do cartão

O detalhe usa cabeçalho e indicadores StatTile como no detalhe de Contas: nome, bandeira/final,
dias do ciclo, limites cadastrado e disponível e ação Editar cartão reutilizando o formulário.
Cartões arquivados têm tag de status. Os limites continuam informativos; valores ausentes mostram
um traço e limite disponível negativo usa o token de erro.

Faturas e Parcelamentos ficam em abas, começando em Faturas. Cada uma preserva seus filtros e
ordenação ao alternar. Situação e período de vencimento ficam visíveis em Faturas; descrição e
categoria ficam visíveis em Parcelamentos. Faixas de valores abrem sobre a tabela num painel
elevado, com indicador de filtros ativos e ação Limpar apenas para a aba atual. Limites iguais a
zero são válidos. Datas aparecem em DD/MM/AAAA e são comparadas internamente como datas ISO.

Linhas de fatura reúnem mês e situação; vencimento e valor restante passam para linhas secundárias
em telas pequenas. Clicar na linha ou no mês abre o detalhe existente da fatura. Parcelamentos
reúnem descrição/categoria, progresso pago/total e valores total e restante. Cada aba tem uma tabela
com borda, contagem no rodapé e cabeçalho fixo com rolagem interna no desktop. No celular, a página
rola sem transbordamento horizontal. Resultados vazios e cartão indisponível têm mensagens próprias.
Contratos de backend e fluxos de detalhe/pagamento de fatura permanecem iguais.

A tabela do detalhe da fatura também se adapta ao celular: descrição e valor continuam como
colunas, enquanto data, parcela, categoria, tipo e situação passam para baixo da descrição conforme
suas colunas são ocultadas. Descrições longas são truncadas; ações do cabeçalho quebram linha e os
indicadores ficam empilhados no celular.

### Limite disponível

`GET /cards/{id}/available-limit` retorna `credit_limit − Σ(totais de faturas não pagas)` quando há
limite (senão `null`). O limite é **informativo** — não há bloqueio ao estourá-lo (D8: consistência
cartão/fatura é query, não invariante).

## Ciclo de vida da fatura

A fatura é aggregate próprio (D8), uma por `(card_id, reference_month)`. Seu status é sempre
**derivado** dos valores e datas via `SyncAmounts`, nunca setado cegamente.

```
open ──(closing_date atingida / job / fechamento manual)──▶ closed
closed ──pagamento parcial──▶ partially-paid ──quitação total──▶ paid (terminal via pagamento)
closed | partially-paid ──(vencida, resta saldo)──▶ overdue ──pagamento──▶ paid
closed | partially-paid | overdue ──reopen──▶ (status recomputado)
```

`SyncAmounts(total, paid, today)` deriva o status nesta ordem:
1. `remaining <= 0` → **paid** (seta `paid_at`).
2. passou do `due_date` **e** fechada → **overdue** (seta `overdue_at`).
3. fechada **e** `paid > 0` → **partially-paid**.
4. fechada → **closed**.
5. senão → **open**.

`RemainingAmount = max(0, total − paid)`. `IsClosedToNewPurchases` = qualquer status que não `open`.

### Resolução de fatura (em qual fatura uma compra cai)

`StatementResolver.Resolve(card, purchaseDate)`:
- Se `purchaseDate.Day <= closing_day` → mês de referência é o mês da compra; senão o mês **seguinte**
  (compra após o fechamento rola para a próxima fatura).
- `closing_date` = `closing_day` do mês de referência.
- `due_date` = `due_day`; se `due_day <= closing_day`, o vencimento cai no mês **seguinte** ao de
  referência (senão no mesmo mês).
- A fatura-alvo é criada sob demanda se ainda não existe; o usuário pode forçar outra fatura.

### Fechamento e reabertura

- **Automático:** o job `StatementLifecycleService` fecha faturas com `closing_date <= hoje` e marca
  `overdue` após o vencimento (ver [Jobs e integração](jobs-and-integration.md)).
- **Fechamento manual:** `POST /statements/{id}/close` — no-op se não estiver aberta.
- **Reabertura:** `POST /statements/{id}/reopen` — reabre fatura fechada/parcialmente-paga/vencida
  para novas compras, limpa `closed_at` e recomputa o status. No-op se ainda aberta ou já totalmente
  paga.

## Pagar uma fatura

`POST /statements/{id}/pay` com uma conta e valor → `PayStatementCommand`:

- Valor deve ser `> 0`; a conta deve existir e **não estar arquivada**.
- **Multi-moeda:** se a moeda da conta ≠ moeda do cartão, um `fxRate` explícito é obrigatório
  (`MissingFxRate` senão).
- Cria um lançamento `card-statement-payment` na conta (sinal −1, dinheiro sai da conta), ligado à
  fatura via `paid_statement_id`, com a categoria de sistema `credit-card-payment`. Sem descrição do
  usuário, renderiza uma descrição de sistema localizada (ex.: "Pagamento — junho 2026").
- Aplica o pagamento via `StatementAmountSync` (`paidDelta = +valor`) e re-deriva o status.
- **Pagamentos parciais e múltiplos são suportados** — a fatura fica `partially-paid` até o pago
  acumulado quitar o total, então `paid`.
- Auditoria: `transaction.created` + `statement.payment-received`, mais `statement.paid` quando esse
  pagamento foi o que quitou totalmente.

O pagamento agrupa conta e valor obrigatórios e recolhe data/descrição opcionais em uma seção
nomeada. Preserva o saldo restante preenchido e envia null nos opcionais vazios. O modal de
640 px empilha no celular e tem rolagem interna quando necessário.

## Quitar sem caixa (write-off de onboarding)

`POST /statements/{id}/settle` → `SettleStatementCommand`:

- Propósito: quitar o saldo de uma fatura **pré-Pandora** sem debitar nenhuma conta (usado ao trazer
  faturas históricas que já estavam pagas antes de o usuário começar a usar o Pandora).
- Cria um lançamento `statement-writeoff` (sinal **0**) com **nenhum destino de conta ou fatura** —
  só `paid_statement_id`. É uma linha durável do ledger (sobrevive a recomputações de `paid_amount`),
  a contrapartida de uma dívida pré-Pandora, espelhando como o `opening-balance` não tem contraparte.
- Quita o **valor restante inteiro** → leva a fatura direto a `paid`. Falha com `NothingToSettle` se
  não resta nada.
- Auditoria: `transaction.created` + `statement.settled-without-cash`.

## Compras e estornos na fatura

Uma **compra** de cartão é um `expense` (sinal de fatura +1) na fatura resolvida; um
**estorno/crédito** é um `refund` (sinal de fatura −1) que reduz o total. Uma fatura `closed` rejeita
novas compras (rolam para a próxima). O cache `total_amount` da fatura é recomputado
transacionalmente a cada mutação via `StatementAmountSync` e sempre é igual à soma das transações.
Ver [Lançamentos](transactions.md) e [Parcelamento](installments.md).

## Tela de detalhe da fatura

O detalhe segue os padrões compartilhados: mês de referência, fechamento/vencimento e status no
cabeçalho, indicadores de total/pago/restante e tabela compacta de lançamentos. Pagar continua como
ação principal; fechar, reabrir e quitar sem movimentar saldo mantêm a disponibilidade existente,
com confirmação para quitar. Os totais usam a moeda do cartão, inclusive em faturas sem lançamentos.

Busca, período e status ficam visíveis. Tipo, categoria e limites de valor ficam em um painel
sobreposto com contagem de filtros ativos. Os campos são controlados, sobrevivem ao fechamento do
painel e são limpos juntos. A tabela preserva ordenação por várias colunas, exibe todos os itens
filtrados com contagem no rodapé e cabeçalho fixo na rolagem do desktop. No celular, a página rola
e os metadados secundários ficam abaixo da descrição. Anexos abrem em drawer separado e só carregam
quando solicitados; envio, associação da fila, abertura e remoção continuam disponíveis nele.
Os controles de anexos quebram em telas estreitas; nomes e notas longos são truncados, com
texto completo preservado no atributo title.

## API

| Método | Rota | Propósito |
|---|---|---|
| GET / POST / PUT / DELETE | `/cards`, `/cards/{id}` | CRUD (excluir só sem histórico) |
| POST | `/cards/{id}/archive` · `/unarchive` | Arquivar/desarquivar |
| GET | `/cards/{id}/statements` | Faturas do cartão |
| GET | `/cards/{id}/installment-plans` | Planos de parcelamento do cartão |
| GET | `/cards/{id}/available-limit` | Limite − faturas não pagas |
| PUT | `/cards/{id}/tags` | Substituir conjunto de tags |
| GET | `/statements/{id}` | Detalhe + lançamentos |
| POST | `/statements/{id}/pay` | Pagar de uma conta |
| POST | `/statements/{id}/settle` | Write-off sem caixa (onboarding) |
| POST | `/statements/{id}/close` | Fechamento manual |
| POST | `/statements/{id}/reopen` | Reabrir para novas compras |
| PUT | `/statements/{id}/tags` | Substituir conjunto de tags |

## Eventos de auditoria

`card.created/updated/archived/unarchived`; `statement.created`, `statement.closed`,
`statement.reopened`, `statement.payment-received`, `statement.paid`, `statement.overdue`,
`statement.settled-without-cash`.
