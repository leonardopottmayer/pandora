# Importação

[← Voltar ao índice](README.md) · Aggregates: `ImportFile`, `ImportRow`, `ImportLayout` · Tabelas: `fin012`–`fin014` · API: `/imports`, `/import-layouts`

---

## Contexto de negócio

O usuário traz histórico existente para o Pandora enviando **arquivos bancários** — **OFX** (extratos
e faturas) e **CSV** (extratos e faturas de cartão). O pipeline faz o parsing do arquivo, deduplica e
concilia cada linha contra o que já está no ledger, e coloca **sugestões** no
[inbox](recurrences-and-inbox.md) para revisão. Nada é postado automaticamente — o usuário aprova,
edita, rejeita ou vincula cada linha.

## Pipeline

```
upload → ImportFile(received) → [job ImportParsingService] → parse linhas → dedup/conciliação
      → gera sugestões (PendingTransaction) → usuário revisa no inbox → completed
```

- **Upload** (`POST /imports`, multipart): o destino é uma **conta XOR um cartão**, um layout opcional
  (auto-detectado se omitido) e uma **data de corte** (cutoff) opcional. Cria um `ImportFile` em
  `received`, guardando os bytes brutos (`file_content`) e um `correlation_id` que amarra a auditoria
  da importação inteira. `file_hash` (sha256) é guardado de forma **informativa** — a UI pode avisar
  sobre upload duplicado, mas reimportar o mesmo arquivo é permitido de propósito (para reconstruir
  sugestões).
- **Job de parsing** (`ImportParsingService`): pega arquivos `received`, escolhe o parser pelo
  formato, extrai `ImportRow`s (bruto preservado em `raw_data`, estruturado em `parsed_payload`). A
  falha de uma linha não aborta o arquivo — a linha vira `error`. Contadores
  (`total/parsed/error/duplicate/suggestion_rows`) são atualizados durante a execução; `retry_count`
  suporta retry.
- **Data de corte:** linhas datadas **antes** de `cutoff_date` são puladas (sem sugestão) — para que
  importar um arquivo histórico longo no go-live não inunde o inbox com movimentos pré-onboarding.
  NULL = importar tudo.
- **Status:** `received → parsing → completed` (ou `failed`, ou `aborted` quando o usuário descarta).
  `POST /imports/{id}/abort`, `POST /imports/{id}/retry`.

## Status do arquivo e da linha

- **ImportFile:** `received | parsing | completed | failed | aborted`.
- **ImportRow:** `pending | suggestion-created | skipped | error`.

## Layouts (`fin012`)

Um **layout** é um perfil de parsing guardado como `config` JSONB, para os parsers ficarem genéricos e
os quirks por banco viverem em dados. Layouts de sistema têm `user_id NULL` e um `layout_code` único
global. O `ILayoutDetector` auto-escolhe um layout para o arquivo enviado quando nenhum é informado.

Layouts de sistema seed (bancos brasileiros):

| Código do layout | Banco | Formato | Destino |
|---|---|---|---|
| `viacredi-ofx` | Viacredi | OFX | conta |
| `viacredi-account-csv` | Viacredi | CSV | conta |
| `nubank-card-ofx` | Nubank | OFX | cartão |
| `nubank-account-ofx` | Nubank | OFX | conta |
| `nubank-card-csv` | Nubank | CSV | cartão |
| `nubank-account-csv` | Nubank | CSV | conta |
| `inter-ofx` | Banco Inter | OFX | conta |
| `itau-account-ofx` | Itaú | OFX | conta |
| `itau-card-csv` | Itaú | CSV | cartão |

**Config OFX** captura quirks: `descriptionField` (NAME/MEMO), `amountIsAlwaysAbsolute`,
`invertAmount`, `treatPaymentAsDebit` e uma lista `quirks` (`multiple-banktranlist`, `comma-decimal`,
`empty-fitid`, `fitid-shared-with-secondary`, `no-closing-tags`, …).

**Config CSV** captura a estrutura: `delimiter`, `encoding`, `isMultiSection`, `dateColumn`,
`dateFormat`, `amountColumn`, `amountDecimalSeparator`, `descriptionColumn`, `identifierColumn`,
`signColumn` + `creditSignValue`/`debitSignValue`, `amountIsAlwaysPositive`,
`positiveAmountIsExpense`, e **`installmentPatterns`** (regexes para detectar uma parcela na
descrição, ex.: `(\d+)/(\d+)`, `- Parcela (\d+)/(\d+)`).

Layouts do usuário (`user_id` setado) estão reservados para uma fase futura; hoje só há seed de
layouts de sistema.

## Deduplicação e conciliação (três níveis)

O `IDuplicateDetector` classifica cada linha contra linhas de importação e transações existentes
(decisão de design D9). Uma chave de dedup é um sha256 de campos de identidade: `dest:fitid:<external_id>`
quando existe um FITID/identificador, senão um hash de conteúdo
`dest:hash:<data>:<valor>:<desc-normalizada>`.

| Nível | Gatilho | Comportamento |
|---|---|---|
| **Certa** (`certain`) | Mesmo FITID/`external_id`, ou mesma chave de dedup, já importado para este usuário + destino. | Uma sugestão ainda é gerada, mas **ligada** à entidade existente (`matched_transaction_id`/pendente). A UI mostra a relação; o usuário decide. Um vínculo manual confirmado pelo usuário vence na resolução do link. |
| **Suspeita** (`suspected`) | Sem casamento exato, mas existe uma transação dentro de **±2 dias** e com o **mesmo valor** (tolerância 0.01). | Uma sugestão **sinalizada** (`duplicate_of_transaction_id`); o usuário aprova (lançar mesmo assim) ou rejeita/vincula. |
| **Nova** (`new`) | Sem casamento. | Uma sugestão normal. |
| **Casada** (`matched`) | A linha concilia com um pendente *esperado* (gerado por recorrência / agendado). | Uma sugestão de **confirmação** (`matched_pending_transaction_id`) — aprovar confirma/vincula o esperado em vez de duplicar. |

A janela de ±2 dias e a tolerância de valor são heurísticas atuais (calibração é um ponto em aberto
conhecido).

## Extração do marcador de parcela (implementado)

Para um CSV/OFX de fatura de cartão que traz só a parcela corrente (ex.: `LOJA X 03/12`, R$ 100), o
parser aplica os `installmentPatterns` do layout para extrair `installment_number = 3` e
`installment_count = 12` no `parsed_payload` e no `ImportRow`/sugestão. Essa parte está implementada
(`OFXParser`/`CsvParser`). O usuário vê esses valores na revisão.

É até onde vai hoje: **aprovar** essa sugestão (`ApprovePendingTransactionCommand`) ignora
`installment_number`/`installment_count`/`matched_installment_plan_id` e simplesmente cria uma
transação simples — não existe matcher que a ligue a um `InstallmentPlan` existente, não existe
criação de plano com `origin = import`, e não existe geração das parcelas futuras projetadas. O design
para esse fluxo completo é:

1. Na aprovação, um matcher de parcelas procuraria plano existente no cartão com a mesma
   `normalized_description`, mesmo count, valor de parcela compatível e uma posição livre: achou → a
   transação aprovada vira a parcela N desse plano; não achou → um novo plano com `origin = import`,
   `total_amount = valor × count` (`total_is_estimate = true`) e `first_reference_month` inferido
   retroativamente.
2. As **parcelas futuras** (N+1..count) seriam geradas como transações com `origin = projection` nas
   faturas seguintes.
3. As **parcelas passadas** (1..N−1) **não** seriam geradas automaticamente.
4. A importação do mês seguinte de `LOJA X 04/12` conciliaria com a parcela projetada em vez de
   duplicá-la.

Nada disso (passos 1-4) está implementado — ver [Parcelamento](installments.md) e
[Status de Implementação](implementation-status.md). O valor `EntryOrigin.Projection` e os campos de
`ImportRow`/`PendingTransaction` que dariam suporte a isso (`matched_installment_plan_id`, etc.) já
existem no schema, sem uso.

## API

| Método | Rota | Propósito |
|---|---|---|
| POST | `/imports` | Upload (multipart: destino, layout opcional, cutoff opcional) |
| GET | `/imports` | Listar arquivos de importação |
| GET | `/imports/{id}` | Status + contadores |
| GET | `/imports/{id}/rows` | Linhas com dado bruto + resultado do dedup |
| POST | `/imports/{id}/abort` · `/retry` | Descartar / reprocessar |
| GET | `/import-layouts` | Layouts de sistema |

## Eventos de auditoria

O pipeline de importação registra eventos sob o `correlation_id` do arquivo (arquivo recebido,
parsing, resultados de linha, conclusão). Sugestões produzidas das linhas usam os eventos padrão
`pending.created`; a trilha da importação inteira é recuperável por `correlation_id`. Ver
[Auditoria e proveniência](audit-and-provenance.md).
