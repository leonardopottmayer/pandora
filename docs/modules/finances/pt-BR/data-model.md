# Modelo de dados

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Lançamentos](transactions.md)

Schema PostgreSQL **`finances`**. Convenções em todas as tabelas: PK `uuid DEFAULT uuid_generate_v7()`,
`TIMESTAMPTZ` para timestamps, colunas de auditoria `created_by/created_at/updated_by/updated_at`,
constraints nomeadas (`pk_finXXX`, `uq_finXXX_*`, `fk_finXXX_*`, `ck_finXXX_*`), enums como `VARCHAR` +
`CHECK`. Toda tabela de dado do usuário tem `user_id NOT NULL` e índice por ele.

Migrations em `migrations/migrations/finances/`.

## Catálogo de tabelas

| # | Tabela | Conteúdo |
|---|---|---|
| fin001 | `account` | Contas |
| fin002 | `system_category` | Categorias do sistema (seed) |
| fin003 | `user_category` | Categorias do usuário |
| fin004 | `tag` | Tags |
| fin005 | `tag_link` | Vínculo polimórfico tag ↔ entidade |
| fin006 | `card` | Cartões de crédito |
| fin007 | `card_statement` | Faturas |
| fin008 | `transaction` | O ledger |
| fin009 | `installment_plan` | Planos de parcelamento |
| fin010 | `recurring_transaction` | Templates de recorrência |
| fin011 | `pending_transaction` | Staging / sugestões do inbox |
| fin012 | `import_layout` | Perfis de parsing |
| fin013 | `import_file` | Arquivos importados |
| fin014 | `import_row` | Linhas parseadas |
| fin015 | *(reservado)* | Regras de categorização — **ainda não implementado** |
| fin016 | `audit_event` | Log de auditoria append-only |
| fin017 | `attachment` | Arquivos de um lançamento, sugestão ou fatura (boleto, comprovante, nota fiscal), ou na fila |
| fin018 | `file_blob` | Os bytes por trás da fin017 |
| fin019 | `card_number` | Números físico e virtuais de um cartão |
| fin020 | `system_asset_class` | Tipos de investimento que o sistema traz (semeados) |
| fin021 | `asset` | O que o usuário tem: uma ação, um produto de CDB, uma moeda |
| fin022 | `position` | Um ativo dentro de uma conta ([Investimentos](investments.md)) |
| fin023 | `position_movement` | O que aconteceu com uma posição; é dono do seu lançamento |
| fin024 | `position_valuation` | Valor informado de uma posição numa data |
| fin025 | `user_asset_class` | As classes de ativo do próprio usuário |

---

## fin001_account

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `name` | varchar(100) NOT NULL | único por usuário |
| `type` | varchar(20) NOT NULL | `cash\|checking\|savings\|international\|crypto\|investment\|benefit\|other` |
| `currency` | varchar(10) NOT NULL | ISO 4217 ou ticker crypto; imutável |
| `benefit_wallet` | varchar(20) NULL | `food\|meal\|flexible`; só em conta `benefit` |
| `institution` | varchar(100) NULL | |
| `description` | varchar(255) NULL | |
| `color` / `icon` | varchar | exibição |
| `display_order` | int NOT NULL DEFAULT 0 | |
| `archived_at` | timestamptz NULL | arquivamento soft |

Constraints: `uq_fin001_user_name (user_id, name)`, `ck_fin001_type`, `ck_fin001_benefit_wallet`
(NULL a menos que `type = 'benefit'`). Índice `ix_fin001_user_id`.

## fin002_system_category

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `code` | varchar(60) NOT NULL | **único** global (`housing`, `rent`, …) |
| `name` | varchar(100) NOT NULL | |
| `transaction_nature` | varchar(10) NOT NULL | `expense\|income` |
| `parent_category_id` | uuid NULL → fin002 | 2 níveis máx. |
| `color` / `icon` | varchar | |
| `display_order` | int NOT NULL | |
| `is_other` | boolean NOT NULL DEFAULT false | filho fallback do grupo |
| `is_active` | boolean NOT NULL DEFAULT true | |
| `notes` | varchar(255) NULL | |

`created_by NULL` marca linhas de seed. Constraints: `uq_fin002_code`, `fk_fin002_parent_category_id`,
`ck_fin002_transaction_nature`. Populado por migration (ver [Categorias e tags](categories-and-tags.md)).

## fin003_user_category

Mesma forma da fin002 mas escopada a `user_id`, sem `code`/`is_other`/`notes`, `parent_category_id`
auto-referenciando `fin003`. Único `uq_fin003_user_name_parent (user_id, name, parent_category_id)`,
`ck_fin003_transaction_nature`.

## fin004_tag / fin005_tag_link

**fin004_tag:** `id`, `user_id`, `name varchar(50)`, `color varchar(20)`. Único `(user_id, name)`.

**fin005_tag_link:** `id`, `tag_id → fin004 (ON DELETE CASCADE)`, `entity_type varchar(30)`,
`entity_id uuid` (sem FK física — polimórfico, integridade garantida na aplicação). Único
`(tag_id, entity_type, entity_id)`; índice `(entity_type, entity_id)`.
`ck_fin005_entity_type`: `account | card | card-statement | transaction | recurring-transaction | pending-transaction`.

## fin006_card

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `name` | varchar(100) | único por usuário |
| `brand` | varchar(50) NULL | visa, mastercard, … |
| `last_four` | varchar(4) NULL | **não é mais usado** (desde 2026-11): copiado para a fin019 como número físico; sai numa release futura |
| `credit_limit` | numeric(20,8) NULL | `>= 0` |
| `closing_day` | int NOT NULL | **1..28** |
| `due_day` | int NOT NULL | **1..28** |
| `currency` | varchar(10) NOT NULL | imutável |
| `account_id` | uuid NOT NULL → fin001 | conta a que o cartão pertence; o banco dela roteia imports |
| `archived_at` | timestamptz NULL | |

Constraints: `uq_fin006_user_name`, `ck_fin006_closing_day/due_day (BETWEEN 1 AND 28)`,
`ck_fin006_credit_limit`, `fk_fin006_account_id`. Índices `(user_id, archived_at)`, `(account_id)`.

> A faixa 1..28 evita ambiguidade com o tamanho do mês (nenhum cartão fecha no dia 30 de fevereiro).

## fin019_card_number

Os números de um cartão. Todos caem na mesma fatura do cartão, por isso são filhos da fin006, e não
cartões próprios.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` / `card_id → fin006` | uuid | |
| `kind` | varchar(20) NOT NULL | `physical\|virtual` |
| `last_four` | varchar(4) NOT NULL | 4 dígitos, único por cartão |
| `nickname` | varchar(50) NULL | ex.: "Uber", "assinaturas" |
| `archived_at` | timestamptz NULL | preenchido em vez de excluir quando algum lançamento usou o número |

Constraints: `ck_fin019_kind`, `ck_fin019_last_four (~ '^[0-9]{4}$')`, `uq_fin019_card_last_four`,
`fk_fin019_card_id`.

## fin007_card_statement

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` / `card_id → fin006` | uuid | |
| `reference_month` | varchar(7) | `yyyy-MM`, uma por cartão+mês |
| `closing_date` / `due_date` | date | |
| `status` | varchar(20) | `open\|closed\|partially-paid\|paid\|overdue` |
| `total_amount` | numeric(20,8) DEFAULT 0 | cache; verdade = Σ transações da fatura |
| `paid_amount` | numeric(20,8) DEFAULT 0 | `>= 0` |
| `closed_at` / `paid_at` / `overdue_at` | timestamptz NULL | |

Constraints: `uq_fin007_card_reference_month`, `ck_fin007_status`, `ck_fin007_reference_month (~ '^\d{4}-\d{2}$')`,
`ck_fin007_paid_amount`, `fk_fin007_card_id`. Índices `(user_id, status, due_date)`, `(card_id, closing_date)`.

## fin008_transaction — o ledger

A tabela central. Construída ao longo de várias migrations; forma final:

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | `user_id` denormalizado para consulta |
| `account_id` | uuid NULL → fin001 | destino (XOR) |
| `card_statement_id` | uuid NULL → fin007 | destino (XOR) |
| `card_id` | uuid NULL → fin006 | denormalizado quando em fatura |
| `card_number_id` | uuid NULL → fin019 | qual número do cartão foi usado; opcional, a importação deixa vazio |
| `paid_statement_id` | uuid NULL → fin007 | fatura paga/quitada por este lançamento |
| `kind` | varchar(30) NOT NULL | ver [Lançamentos](transactions.md) |
| `status` | varchar(10) DEFAULT 'posted' | `pending\|posted\|void` |
| `amount` | numeric(20,8) NOT NULL | `> 0` |
| `currency` | varchar(10) NOT NULL | = moeda do destino |
| `occurred_on` | date NOT NULL | data do fato (civil, sem TZ) |
| `description` | varchar(255) NOT NULL | |
| `system_description` | jsonb NULL | para lançamentos do sistema (renderizado na leitura) |
| `payee` | varchar(150) NULL | |
| `notes` | text NULL | |
| `system_category_id` | uuid NULL → fin002 | |
| `user_category_id` | uuid NULL → fin003 | |
| `transfer_group_id` | uuid NULL | liga o par out/in |
| `fx_rate` | numeric(20,10) NULL | transferência/pagamento multi-moeda |
| `installment_plan_id` | uuid NULL → fin009 | |
| `installment_number` | smallint NULL | `>= 1` |
| `origin` | varchar(15) DEFAULT 'manual' | `manual\|import\|recurrence\|projection\|reversal` |
| `reversed_transaction_id` | uuid NULL → fin008 | o original que este lançamento estorna |
| `pending_transaction_id` | uuid NULL → fin011 | proveniência |
| `recurring_transaction_id` | uuid NULL → fin010 | proveniência |
| `posted_at` / `voided_at` | timestamptz NULL | |
| `void_reason` | varchar(255) NULL | |

**Constraints principais:**
- `ck_fin008_kind` — a lista completa de kinds (11 kinds, incl. `statement-writeoff`).
- `ck_fin008_status`, `ck_fin008_origin`, `ck_fin008_amount (> 0)`.
- `ck_fin008_target_xor` — um lançamento normal tem conta **XOR** fatura; um `statement-writeoff` não
  tem **nenhum** (só `paid_statement_id`).
- `ck_fin008_paid_statement_account_only` — `paid_statement_id` só é setado por
  `card-statement-payment` (com conta) ou `statement-writeoff` (com nenhum).
- `ck_fin008_installment_pairing` — `installment_plan_id` e `installment_number` ambos setados ou ambos nulos.
- `uq_fin008_reversed_transaction_id` — uma reversão por transação.
- `ck_fin008_reversed_transaction_not_self`.

**Índices:** `(user_id, occurred_on)`, `(account_id, status, occurred_on)`,
`(card_statement_id, status, occurred_on)`, `(card_id, occurred_on)`, `(paid_statement_id)`,
`(transfer_group_id)`, `(installment_plan_id)`, `(pending_transaction_id)`,
`(recurring_transaction_id)`, `(reversed_transaction_id)`.

## fin009_installment_plan

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` / `card_id → fin006` | uuid | |
| `origin` | varchar(10) DEFAULT 'manual' | `manual\|import` — hoje só `manual` é gravado; `import` está reservado no schema (ver [Parcelamento](installments.md)) |
| `total_amount` | numeric(20,8) | `> 0` |
| `total_is_estimate` | boolean DEFAULT false | reservado para planos inferidos de importação (ainda não produzido) |
| `installment_count` | smallint | `>= 2` |
| `first_reference_month` | varchar(7) | `yyyy-MM` |
| `description` | varchar(255) | |
| `normalized_description` | varchar(255) | descrição sem o marcador de parcela — chave de casamento |

Índice `(card_id, normalized_description)`.

## fin010_recurring_transaction

Template + regra + cursor de execução.

- **Template:** `account_id`/`card_id` (XOR, `ck_fin010_destination`), `kind`, `amount` (NULL =
  variável), `amount_is_estimate`, `description`, `payee`, `system_category_id`, `user_category_id`.
- **Regra:** `frequency` (`daily|weekly|monthly|yearly`), `interval (>=1)`, `day_of_month (1..31)`,
  `weekday (0..6)`, `start_date`, `end_date`, `max_occurrences`.
- **Execução:** `status` (`active|paused|finished`), `auto_post` (bool), `auto_generate` (bool,
  default true), `next_occurrence_on` (cursor), `occurrences_count`.

Índices `(user_id)`, `(status, next_occurrence_on)`.

## fin011_pending_transaction — staging / inbox

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `source` | varchar(15) | `recurrence\|import` |
| `recurring_transaction_id` | uuid NULL → fin010 | proveniência (recorrência) |
| `import_row_id` | uuid NULL | proveniência (importação; FK lógica) |
| **payload** | | `account_id`, `card_id`, `kind`, `amount`, `currency`, `occurred_on`, `description`, `payee`, `notes`, `system_category_id`, `user_category_id`, `suggested_statement_id → fin007` |
| `original_payload` | jsonb NOT NULL | snapshot **imutável** da sugestão inicial |
| `duplicate_of_transaction_id` | uuid NULL | duplicata suspeita/casada de transação existente |
| `duplicate_of_pending_id` | uuid NULL | duplicata suspeita de outra pendente |
| `dedup_status` | varchar(15) NULL | `new\|certain\|suspected\|matched` (só importação) |
| `installment_number` / `installment_count` | smallint NULL | parcela detectada |
| `matched_installment_plan_id` | uuid NULL | plano casado na aprovação |
| `status` | varchar(10) DEFAULT 'pending' | `pending\|approved\|rejected` |
| `decided_at` / `decided_by` | | |
| `rejection_reason` | varchar(255) NULL | também `linked-to-existing-transaction` |
| `transaction_id` | uuid NULL → fin008 | criada na aprovação |

Constraints: `ck_fin011_source`, `ck_fin011_status`, `ck_fin011_recurrence_source` (recorrência ⇒
`recurring_transaction_id`), `ck_fin011_import_source` (importação ⇒ `import_row_id`),
`ck_fin011_dedup_status`. **Idempotência:** único `(recurring_transaction_id, occurred_on)` — uma
recorrência nunca gera a mesma data duas vezes. Índices `(user_id, status)`,
`(recurring_transaction_id)`, `(import_row_id)`.

## fin012_import_layout

Perfis de parsing. Layouts de sistema têm `user_id NULL` e um `layout_code` único global
(`uq_fin012_system_layout_code`). Colunas: `layout_code`, `name`, `bank_name`, `file_format`
(`ofx|csv`), `account_type` (`account|card`), `config jsonb`. O `config` carrega todas as opções
específicas do parser (quirks de OFX, mapeamento de colunas de CSV, separador decimal, convenção de
sinal, `installmentPatterns`). Populado com layouts para Viacredi, Nubank, Banco Inter, Itaú — ver
[Importação](imports.md).

## fin013_import_file

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `layout_id` | uuid NULL → fin012 | NULL quando a detecção falha |
| `account_id` / `card_id` | uuid NULL | destino (XOR, `ck_fin013_destination`) |
| `file_name` | varchar(255) | |
| `file_hash` | varchar(64) | sha256 hex — **informativo, não único** (o usuário pode reimportar de propósito) |
| `file_content` | bytea | bytes brutos (relidos no retry) |
| `file_size` | int | |
| `cutoff_date` | date NULL | onboarding: linhas antes desta data são puladas |
| `correlation_id` | uuid NOT NULL | amarra toda a auditoria da importação |
| `status` | varchar(15) DEFAULT 'received' | `received\|parsing\|completed\|failed\|aborted` |
| contadores | int | `total_rows`, `parsed_rows`, `error_rows`, `duplicate_rows`, `suggestion_rows` |
| `retry_count` | int | tolerância a falhas |
| `error_message` | text NULL | |
| `started_at` / `completed_at` | timestamptz NULL | |

Índices `(user_id, status)`, parcial `(status, created_at) WHERE status='received'` (fila do job),
`(correlation_id)`.

## fin014_import_row

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `import_file_id → fin013` | uuid | |
| `row_index` | int | |
| `raw_data` | text | bytes originais preservados |
| `parsed_payload` | jsonb NULL | interpretação estruturada |
| `external_id` | varchar(255) NULL | FITID ou coluna identificadora do CSV |
| `dedup_key` | varchar(64) NULL | sha256 dos campos de identidade |
| `dedup_status` | varchar(15) DEFAULT 'new' | `new\|certain\|suspected\|matched` |
| `matched_transaction_id` | uuid NULL | transação existente (certain/suspected) |
| `matched_pending_transaction_id` | uuid NULL | sugestão pendente existente (casamento de recorrência) |
| `installment_number` / `installment_count` | smallint NULL | parcela detectada |
| `matched_installment_plan_id` | uuid NULL | |
| `pending_transaction_id` | uuid NULL | a sugestão criada para esta linha |
| `status` | varchar(20) DEFAULT 'pending' | `pending\|suggestion-created\|skipped\|error` |
| `error_message` | text NULL | |

FKs para fin011/fin008 são **lógicas** (sem FK física) para evitar acoplamento entre importações.
Índices `(import_file_id)`, parcial `(dedup_key)`, `(external_id)`, `(pending_transaction_id)`.

## fin017_attachment / fin018_file_blob — anexos

Um arquivo (imagem ou PDF, até 25 MB) em **no máximo um** de `transaction_id` (fin008),
`pending_transaction_id` (fin011) ou `card_statement_id` (fin007) — `ck_fin017_one_owner`. Sem nenhum ele
está **na fila**: um arquivo mandado ao bot do assistente (`FinancesFileQueue`), esperando o usuário
atribuí-lo (`POST /attachments/{id}/assign`, uma vez só); `note` guarda a legenda com que chegou, e o
índice parcial `ix_fin017_queued` atende a fila. `kind` ∈ `bill` (boleto) · `receipt`
(comprovante) · `invoice` (nota fiscal) · `other`. `storage_backend` + `storage_key` dizem onde estão os
bytes: hoje `Database`, um id de linha na `fin018_file_blob` (a tabela de blobs do próprio módulo, via o
`IFileStorage` compartilhado com a chave `finances`); linhas gravadas antes de um backend S3 continuam
lendo do banco, sem migration.

O boleto do mês é anexado à sugestão que a recorrência gerou; **aprovar**, **vincular** ou **transferir**
a sugestão leva os anexos para o lançamento, onde o comprovante se junta a eles. Rejeitar os mantém na
sugestão rejeitada. Lançamentos nunca são excluídos de fato (cancelar mantém os arquivos). Adicionar/remover
(e atribuir um arquivo da fila) grava `transaction.attachment-added|removed` (ou `pending.…`, `statement.…`)
no dono; um arquivo na fila não tem dono nem trilha.

## fin016_audit_event — append-only

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | `user_id` = dono do dado |
| `actor_user_id` | uuid NULL | quem agiu (NULL = sistema/job) |
| `entity_type` | varchar(40) | `transaction`, `pending-transaction`, `import-file`, … |
| `entity_id` | uuid | |
| `event_type` | varchar(60) | `transaction.created`, `pending.approved`, … |
| `data` | jsonb NULL | diff `{ field: { old, new } }` e/ou detalhe do evento |
| `correlation_id` | uuid NULL | agrupa tudo de uma operação |
| `occurred_at` | timestamptz NOT NULL | |

Índices `(entity_type, entity_id, occurred_at)`, `(user_id, occurred_at)`. Sem `UPDATE`/`DELETE` por
política de aplicação. Ver [Auditoria e proveniência](audit-and-provenance.md).

---

As tabelas de investimentos abaixo ([Investimentos](investments.md), parte 1) vieram depois da 1.0 só com
migrations novas, sem tocar nos dados existentes. Valores de enum usam hífen.

## fin020_system_asset_class

As classes de ativo que o sistema traz: globais, semeadas, só leitura, como a `fin002_system_category`.
As classes do usuário ficam na `fin025`, como as categorias do usuário ficam na `fin003`.

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `code` | varchar(40) NOT NULL | único (`cdb`, `stock`…); a UI traduz por ele |
| `name` | varchar(60) NOT NULL | |
| `behavior` | varchar(20) NOT NULL | `unit\|application` |
| `display_order` | int NOT NULL | |

Semeadas: `stock`, `fii`, `etf`, `bdr`, `fund`, `pension`, `crypto`, `currency` (`unit`); `cdb`, `lci`, `lca`,
`treasury`, `debenture`, `coe` (`application`); `other` (`unit`).

## fin025_user_asset_class

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `name` | varchar(60) NOT NULL | único por usuário (`uq_fin025_user_name`) |
| `behavior` | varchar(20) NOT NULL | `unit\|application`; fixo na criação |
| `archived_at` | timestamptz NULL | |

## fin021_asset

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `system_asset_class_id` | uuid NULL → fin020 | a classe, quando é do sistema |
| `user_asset_class_id` | uuid NULL → fin025 | a classe, quando é do usuário |
| `name` | varchar(100) NOT NULL | único por usuário |
| `code` | varchar(20) NULL | ticker ou código do produto; único por usuário e classe quando preenchido |
| `isin` | varchar(12) NULL | |
| `issuer` | varchar(100) NULL | ex.: Banco Inter |
| `currency` | varchar(10) NOT NULL | o mesmo `CurrencyCode` das contas |
| `archived_at` | timestamptz NULL | |

Constraints: `ck_fin021_one_class` (exatamente uma das duas colunas de classe: diferente das duas
categorias de um lançamento, nunca as duas), `uq_fin021_user_name`, `uq_fin021_user_class_code` (código único por usuário e classe).

## fin022_position

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `account_id` | uuid NOT NULL → fin001 | |
| `asset_id` | uuid NOT NULL → fin021 | |
| `behavior` | varchar(20) NOT NULL | copiado da classe na criação: `unit\|application` |
| `status` | varchar(20) NOT NULL | `open\|closed` |
| `quantity` | numeric(28,10) NULL | só `unit`; aceita frações (cripto, cotas de fundo) |
| `average_cost` | numeric(28,10) NULL | só `unit`; por unidade, com custos |
| `invested_amount` | numeric(18,2) NULL | só `application`; o que ainda está aplicado |
| `applied_on` | date NULL | só `application` |
| `index` | varchar(20) NULL | `application`: `cdi\|selic\|ipca\|pre\|other` |
| `index_percent` | numeric(9,4) NULL | ex.: 110 para 110% do CDI |
| `spread_rate` | numeric(9,4) NULL | % ao ano: IPCA + 6,5, ou a taxa prefixada |
| `maturity_on` | date NULL | `application` |
| `closed_at` | timestamptz NULL | |

Constraints: linhas `unit` têm quantidade e custo médio e nenhum campo de aplicação; linhas `application`,
o contrário. Uma posição `unit` **aberta** por (conta, ativo): índice único parcial. Na parte 1 os campos de
taxa são informativos; a parte 2 usa para a curva.

## fin023_position_movement

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` / `position_id → fin022` | uuid | |
| `kind` | varchar(20) NOT NULL | `opening\|buy\|sell\|redemption\|maturity\|income\|split\|come-cotas` |
| `occurred_on` | date NOT NULL | |
| `quantity` | numeric(28,10) NULL | unidades compradas, vendidas ou levadas pelo come-cotas |
| `unit_price` | numeric(28,10) NULL | |
| `gross_amount` | numeric(18,2) NULL | |
| `costs` | numeric(18,2) NOT NULL DEFAULT 0 | corretagem, taxas |
| `tax_withheld` | numeric(18,2) NOT NULL DEFAULT 0 | IR/IOF retido na fonte |
| `net_amount` | numeric(18,2) NULL | o que entrou ou saiu do dinheiro |
| `income_kind` | varchar(30) NULL | `income`: `dividend\|interest-on-equity\|rent\|coupon\|other` |
| `split_factor` | numeric(18,8) NULL | `split`: 2 = desdobramento 1→2, 0,1 = grupamento 10→1 |
| `realized_result` | numeric(18,2) NULL | `sell`/`redemption`/`maturity`: lucro ou prejuízo sobre o custo |
| `quantity_before` / `average_cost_before` / `invested_amount_before` | numeric NULL | a posição antes deste movimento, para desfazer |
| `transaction_id` | uuid NULL → fin008, único | o lançamento ligado no ledger (INV-7) |
| `notes` | varchar(255) NULL | |

## fin024_position_valuation

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` / `position_id → fin022` | uuid | |
| `valued_on` | date NOT NULL | único por posição |
| `value` | numeric(18,2) NOT NULL | valor total da posição nessa data |
| `source` | varchar(20) NOT NULL | `manual\|import\|quote` |

Migrations: `create-table-fin020-system-asset-class`, `insert-fin020-system-asset-classes`,
`create-table-fin025-user-asset-class`, `create-table-fin021-asset`,
`create-table-fin022-position`, `create-table-fin023-position-movement`, `create-table-fin024-position-valuation`.
Nenhuma tabela existente muda.

---

## Relacionamentos (simplificado)

```mermaid
erDiagram
    ACCOUNT ||--o{ TRANSACTION : "lançamentos"
    CARD ||--o{ CARD_STATEMENT : "faturas"
    CARD_STATEMENT ||--o{ TRANSACTION : "lançamentos da fatura"
    TRANSACTION }o--o| SYSTEM_CATEGORY : ""
    TRANSACTION }o--o| USER_CATEGORY : ""
    TRANSACTION }o--o| INSTALLMENT_PLAN : "parcela de"
    TRANSACTION }o--o| TRANSACTION : "estorna"
    TRANSACTION }o--o| PENDING_TRANSACTION : "criada de"
    PENDING_TRANSACTION }o--o| IMPORT_ROW : "sugerida por"
    PENDING_TRANSACTION }o--o| RECURRING_TRANSACTION : "gerada por"
    IMPORT_FILE ||--o{ IMPORT_ROW : "linhas"
    IMPORT_FILE }o--o| IMPORT_LAYOUT : "parseada com"
    TAG ||--o{ TAG_LINK : ""
    TRANSACTION ||--o{ TAG_LINK : "polimórfico"
    TRANSACTION ||--o{ ATTACHMENT : "arquivos"
    PENDING_TRANSACTION ||--o{ ATTACHMENT : "arquivos (vão na aprovação)"
    CARD_STATEMENT ||--o{ ATTACHMENT : "arquivos"
```
