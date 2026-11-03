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
| **Parcelamento** | Só planos manuais, divisão de centavos, cancelar parcela/plano (`fin009`). Planos inferidos de importação + projeções **não** estão implementados — ver abaixo. |
| **Tags** | CRUD de tags + vínculos polimórficos (`fin004`, `fin005`). |
| **Anexos** | Imagens/PDFs num lançamento, numa sugestão ou numa fatura, com tipo (boleto, comprovante, nota fiscal, outro); os arquivos da sugestão vão para o lançamento na aprovação. Arquivos mandados ao bot do assistente no Telegram (com legenda "comprovante", "boleto", "financeiro"…) esperam numa fila no inbox até serem atribuídos (`fin017`, `fin018`). |
| **Recorrências e inbox** | Templates de recorrência + motor de regras, inbox de staging, aprovar/rejeitar/vincular/transferir-do-pendente, job de geração (`fin010`, `fin011`). |
| **Importação** | OFX **e** CSV, seed de layouts de banco + auto-detecção, dedup/conciliação de três níveis, extração do marcador de parcela para `parsed_payload`/`ImportRow` (número/count apenas — sem casamento de plano), data de corte, retry (`fin012`–`fin014`). |
| **Reversibilidade** | Cancelar, desfazer, estornar (todos os casos), proteções de exclusão em conta/cartão. |
| **Leituras de auditoria** | Timeline `/audit` por entidade ou correlation id. |
| **Frontend** | Módulo React (`client-web/src/modules/finances`) cobrindo contas, cartões, faturas, lançamentos, transferências, categorias, tags, recorrências, inbox, importações, auditoria. |
| **Lista e formulário de cartões** | Redesenhados conforme os padrões de tela: linhas compactas e responsivas, menus identificados, confirmação de arquivamento/exclusão, tabela com rolagem e cabeçalho fixo no desktop e modal com campos em pares e opcionais recolhidos. Ver [Tela de cartões](cards-and-statements.md#tela-de-cartões). |
| **Detalhe do cartão** | Cabeçalho com edição, indicadores de limites, abas responsivas de faturas/parcelamentos com filtros e ordenação independentes preservados, filtros secundários de valores sobre a tabela, contagens e rolagem interna. Linhas de fatura abrem o detalhe existente. Ver [Tela de detalhe](cards-and-statements.md#tela-de-detalhe-do-cartão). |

### Adições além do design original

A tela de etiquetas usa lista compacta com busca, menus por linha, exclusão confirmada, amostras
de cor legíveis nos temas e formulário com prévia e opção de limpar a cor. Veja [Tela de etiquetas](categories-and-tags.md#tela-de-etiquetas).

A tela de categorias segue os padrões compartilhados, com abas pessoais/sistema, busca e filtro
de natureza independentes, linhas hierárquicas, menus por linha, formulário responsivo e rolagem
interna no desktop. Veja [Tela de categorias](categories-and-tags.md#tela-de-categorias).

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
| **Planos de parcelamento inferidos de importação + projeções** | `InstallmentPlan` só tem a factory `Origin.Manual` (`CreateManual`); aprovar uma sugestão de importação com marcador de parcela detectado cria uma transação simples, não um plano. Os valores de origem `import`/`projection`, e os campos de casamento em `ImportRow`/`PendingTransaction` (`matched_installment_plan_id`, etc.), existem no schema/domínio mas não são usados. O próprio comentário do agregado `InstallmentPlan` chama isso de "phase 10". |
| **Regras de categorização** (`fin015`) | Auto-categorizar sugestões de importação ("descrição contém UBER → Transporte"). Tabela e casos de uso não criados. |
| **Relatórios** | Endpoints de fluxo de caixa, por categoria, histórico de saldo e agenda de vencimentos não implementados — só existe a timeline de auditoria. |
| **Eventos de integração com Channels** | O projeto `Contracts` está vazio; `StatementClosed`/`StatementDueSoon`/`StatementOverdue`/`ImportCompleted`/`PendingTransactionsGenerated` não são publicados, e não há subscribers/templates no Channels. |
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
