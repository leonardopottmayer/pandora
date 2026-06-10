# Fase 09 — Importação OFX (pipeline base)

> Pré-requisitos: fase 08 (inbox).
> Referência: [finances-module.md](../finances-module.md) §3.7, §6 (fin012–fin014), §7, §8.5, D6, D9.

## Objetivo

Pipeline de importação completo com o formato OFX: upload → parsing assíncrono → dedup/conciliação em três níveis → sugestões no inbox → aprovação com proveniência total. A fase 10 só pluga outro parser nesse pipeline.

## Escopo

### Inclui
- Migrations:
  - `create-table-fin012-import-layout` + seed de layouts OFX de sistema (`user_id NULL`; quirks por banco quando aparecerem);
  - `create-table-fin013-import-file` (hash sha256 único por usuário, `file_content bytea`, status, contadores, `correlation_id`);
  - `create-table-fin014-import-row` (raw preservado, `parsed_payload`, `external_id` FITID, `dedup_status`, `matched_transaction_id`, status);
  - `alter-table-fin011-add-import-columns`: `import_row_id`, `duplicate_of_transaction_id`, `matched_transaction_id`, `suggested_statement_id`, `suggestion_confidence`; CHECK de `source` ampliado para `import`.
- Aggregates `ImportFile` (máquina de estados §7; rows via repositório, nunca carregadas juntas) e `ImportLayout`.
- Ports/adapters: `IImportParser` (estratégia por formato) + parser OFX (encoding, datas, FITID, sinal); `IDuplicateDetector` com os três níveis (D9):
  - duplicata certa: FITID já importado na mesma conta/cartão, ou linha idêntica de arquivo reprocessado → linha visível, sem sugestão, forçável;
  - suspeita: data ±2d + mesmo valor + similaridade de descrição → sugestão sinalizada (`duplicate_of_transaction_id`);
  - conciliação: casa com lançamento esperado (`status = pending`: agendado ou recorrência auto-postada futura) → sugestão de confirmação (`matched_transaction_id`); aprovar **atualiza/posta o existente** em vez de criar.
- Job `ImportParsingService`: processa arquivos `received`; falha de linha não aborta o arquivo; contadores e status atualizados; tudo amarrado pelo `correlation_id` do arquivo.
- Destino conta **e** cartão (extrato de cartão OFX alimenta faturas via `IStatementResolver` → `suggested_statement_id`).
- API: `/imports` (`POST` upload multipart com destino + layout opcional, `GET {id}` status/contadores, `GET {id}/rows` com raw e dedup, `POST {id}/abort`, `POST /rows/{id}/force-suggest` para duplicata certa); `/import-layouts` (`GET` sistema).
- Inbox: sugestões de importação aparecem com badge de duplicata/conciliação e link para o lançamento relacionado; aprovação em modo confirmação implementada.
- Auditoria: `import.file-received/parsing-started/row-parsed/row-failed/row-duplicated/row-matched/completed/failed/aborted`, `pending.approved-as-confirmation`.

### Não inclui
- CSV e layouts customizados, detecção de parcelas (fase 10); regras de categorização (fase 11).

## Critérios de aceite
1. Importar o mesmo arquivo duas vezes: aviso de hash; reprocessando mesmo assim, todas as linhas saem como duplicata certa e nenhuma sugestão é criada.
2. Arquivo com FITID repetido parcialmente: só as linhas novas geram sugestão.
3. Linha parecida com lançamento manual existente (±2d, mesmo valor) gera sugestão sinalizada; aprovar lança mesmo assim, rejeitar ignora — ambos auditados.
4. Linha que casa com agendado pendente: aprovação posta o existente com valores reais (sem nova transação) e liga `transaction_id`.
5. Auditoria de uma importação inteira recuperável por `correlation_id` em uma query, incluindo o raw de cada linha e o que o usuário editou (cenário do §3.9 reproduzido em teste de integração).
6. OFX de cartão sugere lançamentos na fatura correta.
