# Fase 10 — Importação CSV, layouts customizados e parcelas

> Pré-requisitos: fases 06 e 09.
> Referência: [finances-module.md](../finances-module.md) §3.7 (detecção de parcelas), §6 (fin012 config), §8.7, D9.

## Objetivo

CSV nos layouts de cada banco (incluindo perfis criados pelo usuário) e o tratamento de fatura de cartão sem dado estruturado de parcela: detectar `3/12` na descrição, casar/criar o plano e projetar as parcelas futuras.

## Escopo

### Inclui
- Parser CSV plugado em `IImportParser`, dirigido pelo `config` do layout: delimitador, encoding, formato de data, separador decimal, linhas de cabeçalho, mapeamento de colunas, convenção de sinal/inversão.
- Layouts: seed de sistema para os bancos usados + CRUD de layouts do usuário (`/import-layouts`), com endpoint de **preview** (`POST /import-layouts/{id}/preview` com amostra do arquivo → linhas parseadas, sem persistir) para o usuário calibrar o perfil.
- Detecção de parcelas:
  - `installmentPatterns` no config (regex; defaults de sistema: `3/12`, `03/12`, `PARC 3/12`, `3 de 12`);
  - parser extrai `installment_number`/`installment_count` para `parsed_payload` e para a sugestão;
  - migration `alter-table-fin011-add-installment-columns`: `installment_number`, `installment_count`, `matched_installment_plan_id`; campos editáveis/zeráveis na revisão (falso positivo).
- `IInstallmentPlanMatcher` na aprovação (fluxo §8.7):
  - casa com plano existente (mesma `normalized_description`, mesmo count, valor compatível com tolerância, posição livre) → transação vira a parcela N;
  - sem plano → cria com `origin = 'import'`, `total_amount = valor × count` (`total_is_estimate = true`), `first_reference_month` retroativa;
  - parcelas futuras (N+1..count) geradas como transações `pending` com `origin = 'projection'` nas faturas seguintes (CHECK de `origin` em fin008 ampliado);
  - parcelas passadas não são geradas (questão aberta nº 8 — backfill opcional fica fora desta fase).
- Conciliação com projeções: `IDuplicateDetector` passa a casar linha importada com parcela projetada (plano + posição) → sugestão de confirmação; aprovar posta a projeção com os valores reais.
- Visões de fatura: total previsto inclui projeções (separado do total postado).
- Auditoria: `installment-plan.created` (origem import), `installment-attached`, `projection-generated`; layouts auditados como o restante.

### Não inclui
- Outros formatos (futuro); regras de categorização (fase 11).

## Critérios de aceite
1. CSV de fatura com `LOJA X 03/12` → sugestão com parcela 3/12 detectada; aprovação cria plano retroativo (1ª parcela inferida 2 meses antes) + projeções 4..12 nas faturas futuras.
2. Importação do mês seguinte com `LOJA X 04/12` concilia com a projeção (confirmação) — nada duplica; valores reais substituem os projetados.
3. Falso positivo: usuário zera os campos de parcela na revisão e a aprovação cria transação simples.
4. Layout customizado: usuário cria perfil para um CSV novo via preview e importa com sucesso; sinal invertido (`invertSign`) tratado.
5. Projeções aparecem no total previsto da fatura, mas não no `total_amount` postado nem no saldo.
