# Finances — Plano de produto (trabalho restante)

> **Status:** investimentos em **planejamento** (início em 2026-11-23). Nada aqui está construído.
> O que já existe fica nos docs por tópico e em [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/product-plan.md)

---

## Investimentos

### Objetivo

Saber **quanto tenho, onde e em quê**, com valor atualizado, em todos os bancos, corretoras e países:
renda fixa (CDB, LCI, LCA, Tesouro…), ações, FIIs, fundos, ETFs no exterior (Avenue/Nasdaq), moedas
(USD, EUR) e cripto. Flexível para aceitar qualquer classe usada hoje ou no futuro.

### Decisões até agora

| # | Decisão | Por quê |
|---|---|---|
| INV-1 | Construir primeiro o **A, patrimônio**: posições, movimentos e valores. **B, rentabilidade** (vs CDI/IPCA) e **C, impostos** (ganho de capital, DARF, declaração anual) são extensões por cima, fora da primeira entrega. | O A entrega valor rápido e é a base de que B e C precisam. |
| INV-2 | Desde o início, o A guarda o que B e C vão precisar: **cada movimento com data, quantidade, preço unitário e custos** (corretagem, taxas, IOF) e **o valor de cada posição numa data**, não só o último valor. | Rentabilidade e ganho de capital saem disso. Guardar só saldo tornaria B e C impossíveis de reconstruir. |
| INV-3 | **As posições ficam dentro das contas.** Banco ou corretora continua sendo uma conta do Finances; o saldo em dinheiro segue no ledger e as posições ficam penduradas na conta. Comprar move dinheiro para uma posição; vender devolve. Total da conta = dinheiro + posições. | É assim que bancos e corretoras funcionam, e reaproveita transferências, importação e caixa de entrada. |
| INV-4 | **O valor atual de uma posição é informado**, digitado ou importado do relatório de posição da instituição, e guardado como valor numa data. As cotações automáticas vêm depois (parte 2) e só *geram* os mesmos valores datados. | Funciona para qualquer classe, inclusive as sem cotação pública (fundos, COE, previdência). Corta a primeira entrega pela metade. |
| INV-5 | **O que é uma posição:** ações, FIIs, ETFs, cripto e moedas são **uma posição por ativo por conta**, com custo médio. Renda fixa (CDB, LCI, LCA, Tesouro…) é **uma posição por aplicação**, com data, taxa, indexador (CDI, IPCA, prefixado), vencimento e valor informado próprios; a tela pode agrupar aplicações por produto ou emissor. | É como os bancos mostram a renda fixa, então o relatório de posição importado bate linha a linha; o IR da renda fixa (C), os alertas de vencimento e a curva (parte 2) precisam do dado por aplicação. |
| INV-6 | **A carteira começa pela foto de hoje.** Cada posição ganha uma **abertura**: quantidade e custo médio numa data para ativos por unidade; data da aplicação, valor aplicado, taxa, vencimento e valor atual para renda fixa. Os movimentos novos são registrados a partir daí. O histórico completo pode substituir uma abertura depois, quando houver importadores (parte 5). | O custo médio é o que as regras de imposto usam, então uma abertura correta basta para o C; dá para colocar tudo numa tarde a partir dos relatórios de posição das instituições. |
| INV-7 | **Os movimentos comandam o dinheiro.** Cada movimento que mexe em dinheiro gera um lançamento ligado na mesma conta: compra → `investment-contribution` (valor + custos); venda, resgate, vencimento, amortização → `investment-redemption` (líquido); provento → `yield` (líquido). Abertura, come-cotas e desdobramento não geram nada. O lançamento ligado só muda ou é desfeito pelo seu movimento, como o pagamento de fatura. Total da conta = dinheiro do ledger + valor atual das posições abertas. | Mantém o ledger como única fonte de verdade do dinheiro, então posições e dinheiro nunca divergem. |
| INV-8 | **Qualquer conta pode ter posições**, não só as `investment`: um CDB comprado direto da corrente do Inter fica nessa conta. A regra antiga que restringe `investment-contribution`/`-redemption`/`yield` a contas `investment` deixa de valer para lançamentos gerados por movimentos. Contas `investment` continuam para corretoras (XP, Avenue). | Bancos vendem renda fixa direto da corrente; uma conta separada daria dois passos por compra e um saldo artificial. |

### As partes e a ordem

| # | Parte | O que resolve | Depende de |
|---|---|---|---|
| 1 | **Núcleo da carteira** | Ativos, posições por conta, movimentos (compra, venda, aporte, resgate, provento, come-cotas, desdobramento), custo médio, valores informados | — |
| 5 | **Entrada de dados** (primeiro: manual + uma fonte) | Lançamento manual, depois uma fonte de importação | 1 |
| 2 | **Cotações e índices** | Preços de ações/FIIs/cripto, câmbio, séries de CDI/IPCA; renda fixa pela curva | 1 |
| 6 | **Relatórios** | Alocação por classe/instituição/moeda, evolução do patrimônio, rentabilidade vs CDI | 1, 2 |
| 3 | **Multimoeda e exterior** | Avenue/Nasdaq, USD/EUR, cripto, tudo consolidado em reais | 1, 2 (câmbio) |
| 4 | **Impostos** | IR por classe, compensação de prejuízo, DARF mensal, relatório de *Bens e Direitos* | 1, com custo médio e vendas confiáveis |

Impostos vêm por último de propósito: só ficam certos quando custo médio e vendas estão certos.

### Ideias a guardar (ainda não decididas)

**Flexibilidade.** Classe do ativo, moeda, instituição e tipo de rendimento são dados, não código,
então uma classe nova (debênture, CRI, Tesouro) não precisa de migration estrutural.

**A entrada de dados é um pipeline de conectores**, como a importação de extratos que já existe
(layout por fonte, caixa de entrada para revisar). Fontes candidatas, um conector cada:

- lançamento manual;
- notas de corretagem (PDF);
- extratos de posição e de movimentação (CSV/XLSX) de bancos e corretoras;
- relatórios da Área do Investidor da B3;
- APIs de exchanges de cripto;
- extratos da Avenue / corretoras no exterior;
- Open Finance (Pluggy ou similar), mais adiante.

**Cotações e índices (parte 2).** Preços de ações/FIIs/ETFs da B3, cripto e tickers no exterior;
câmbio (PTAX); séries de CDI, Selic e IPCA. Renda fixa pela curva tem armadilhas: dias úteis,
carência, IOF nos primeiros 30 dias, prefixado vs pós-fixado. Fundos, COE e previdência continuam
informados.

**Impostos (parte 4).** As regras mudam com o tempo, então cada regra fica isolada e **datada**, e
cada uma é conferida na fonte oficial antes de ser construída, nunca de memória. Temas a cobrir:
tabela regressiva da renda fixa, isenção de LCI/LCA, isenção na venda de ações até um limite mensal,
day trade, rendimentos e vendas de FII, cripto, investimentos no exterior (regras mudaram em 2024
com a Lei 14.754/2023), come-cotas, compensação de prejuízo por categoria, DARF mensal, *Bens e
Direitos* da declaração anual.

**Rentabilidade (B).** Precisa de cotações históricas desde a primeira compra, ou pelo menos dos
valores informados datados que o INV-2 já guarda.

---

## Parte 1 — Núcleo da carteira: especificação

> **Status:** aprovada em 2026-11-23. Implementa INV-1 a INV-8.

### Conceitos

| Termo | Significado |
|---|---|
| **Classe de ativo** | Que tipo de investimento (CDB, ação, FII…). Classes do sistema, semeadas, mais as do usuário. Cada uma tem um **comportamento**: `unit` (quantidade e custo médio) ou `application` (renda fixa: data, taxa, vencimento). |
| **Ativo** | Algo que o usuário tem: nome, código opcional (ticker ou ISIN), emissor opcional, moeda, classe. |
| **Posição** | Um ativo dentro de uma conta. Ativos `unit`: uma posição aberta por ativo por conta. Ativos `application`: uma posição por aplicação. Aberta enquanto tiver algo, encerrada quando zera. |
| **Movimento** | Algo que aconteceu com uma posição: abertura, compra, venda, resgate, vencimento, provento, desdobramento, come-cotas. Guarda data, quantidade, preço unitário, valor bruto, custos e imposto retido. |
| **Valor informado** | Quanto uma posição vale numa data, com a origem. O mais recente é o valor atual. |

Regras que valem sempre:

- A moeda do ativo tem que ser a moeda da conta (a conversão é a parte 3).
- Conta arquivada não recebe posições nem movimentos novos; conta com posições não pode ser excluída.
- Toda escrita grava um evento de auditoria (`fin016`), como no resto do módulo.

### Modelo de dados

Só tabelas novas, então as migrations só acrescentam (nada muda nos dados existentes). Valores de enum
usam hífen.

**`fin020_asset_class`**

| Coluna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NULL | NULL = classe do sistema (semeada, só leitura) |
| `code` | varchar(40) NULL | só classes do sistema (`cdb`, `stock`…) |
| `name` | varchar(60) NOT NULL | único por usuário (os nomes do sistema são únicos entre si) |
| `behavior` | varchar(20) NOT NULL | `unit\|application` |
| `archived_at` | timestamptz NULL | só classes do usuário |

Semeadas: `stock`, `fii`, `etf`, `bdr`, `fund`, `pension`, `crypto`, `currency` (`unit`); `cdb`, `lci`, `lca`,
`treasury`, `debenture`, `coe` (`application`); `other` (`unit`).

**`fin021_asset`**

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` | uuid | |
| `asset_class_id` | uuid NOT NULL → fin020 | |
| `name` | varchar(100) NOT NULL | único por usuário |
| `code` | varchar(20) NULL | ticker ou código do produto; único por usuário e classe quando preenchido |
| `isin` | varchar(12) NULL | |
| `issuer` | varchar(100) NULL | ex.: Banco Inter |
| `currency` | varchar(10) NOT NULL | o mesmo `CurrencyCode` das contas |
| `archived_at` | timestamptz NULL | |

**`fin022_position`**

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

**`fin023_position_movement`**

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

**`fin024_position_valuation`**

| Coluna | Tipo | Notas |
|---|---|---|
| `id` / `user_id` / `position_id → fin022` | uuid | |
| `valued_on` | date NOT NULL | único por posição |
| `value` | numeric(18,2) NOT NULL | valor total da posição nessa data |
| `source` | varchar(20) NOT NULL | `manual\|import\|quote` |

Migrations: `create-table-fin020-asset-class`, `insert-fin020-system-asset-classes`, `create-table-fin021-asset`,
`create-table-fin022-position`, `create-table-fin023-position-movement`, `create-table-fin024-position-valuation`.
Nenhuma tabela existente muda.

### Regras dos movimentos

Posições `unit` (quantidade q, custo médio c):

| Tipo | Entrada | Efeito na posição | Lançamento no ledger (INV-7) |
|---|---|---|---|
| `opening` | quantidade, custo médio, data | define q e c; tem que ser o primeiro movimento | nenhum |
| `buy` | quantidade, preço unitário, custos | c = (q·c + qtd·preço + custos) / (q + qtd); q += qtd | `investment-contribution`: qtd·preço + custos |
| `sell` | quantidade ≤ q, preço unitário, custos, imposto | q −= qtd; c não muda; resultado = qtd·preço − custos − qtd·c; q = 0 encerra | `investment-redemption`: qtd·preço − custos − imposto |
| `income` | bruto, imposto, tipo | nenhum | `yield`: bruto − imposto |
| `split` | fator | q ·= fator; c /= fator | nenhum |
| `come-cotas` | quantidade levada, imposto | q −= qtd; c não muda | nenhum |

Posições `application` (valor aplicado v):

| Tipo | Entrada | Efeito na posição | Lançamento |
|---|---|---|---|
| `opening` | valor aplicado, data, indexador, taxas, vencimento, valor atual | define v; grava um valor informado com o valor atual | nenhum |
| `buy` | igual à abertura, sem valor atual | define v; primeiro valor informado = v | `investment-contribution`: v + custos |
| `redemption` | bruto, imposto, custos | fração = bruto ÷ valor atual; v −= v·fração; resultado = bruto − v·fração; o valor todo encerra | `investment-redemption`: bruto − imposto − custos |
| `maturity` | bruto, imposto, custos | resgate total; encerra | `investment-redemption` |
| `income` | bruto, imposto, tipo (`coupon`) | nenhum | `yield` |

- Uma posição começa com `opening` (sem dinheiro: ele saiu no passado, INV-6) ou `buy` (o dinheiro sai
  agora). Uma aplicação nova de renda fixa é sempre uma posição nova.
- **Desfazer** remove só o **último** movimento de uma posição: restaura os valores `*_before` e estorna o
  lançamento ligado com o void que já existe. Desfazer o único movimento exclui a posição.
- O lançamento ligado não pode ser editado, estornado nem revertido pelo ledger: a API responde com um
  erro que aponta para a posição.
- Lançamentos gerados por movimentos não passam pela regra que limita esses tipos a contas `investment`
  (INV-8). Lançamentos digitados à mão continuam com a regra.
- Os campos de imposto são **digitados**, como o banco ou a corretora mostram; a parte 1 não calcula
  imposto.

### Valor e totais

- **Valor atual** de uma posição = seu último valor informado. Sem nenhum: o custo (`q·c`, ou `v`),
  mostrado como "sem valor informado".
- Um valor com mais de **30 dias** aparece como desatualizado.
- **Total da conta** = saldo em dinheiro do ledger + valor atual das posições abertas.
  `GET /accounts/{id}/balance` ganha `positionsValue` e `total`; os campos existentes mantêm o sentido.
- Os totais são **por moeda**; nada é convertido (parte 3).

### API (`/api/v{n}/finances`)

| Método | Rota | Faz |
|---|---|---|
| GET / POST | `/asset-classes` | Listar (sistema + próprias) / criar |
| PUT | `/asset-classes/{id}` | Renomear (só próprias) |
| POST | `/asset-classes/{id}/archive` · `/unarchive` | Só próprias |
| GET / POST | `/assets` | Listar (busca, classe) / criar |
| PUT | `/assets/{id}` | Editar |
| GET | `/positions` | Listar (conta, status, classe) com valor atual |
| GET | `/positions/{id}` | Detalhe, movimentos e valores |
| POST | `/positions` | Criar com o primeiro movimento (`opening` ou `buy`) |
| POST | `/positions/{id}/movements` | Adicionar um movimento |
| DELETE | `/positions/{id}/movements/latest` | Desfazer o último movimento |
| POST | `/positions/{id}/valuations` | Informar um valor numa data (mesma data substitui) |
| POST | `/positions/valuations` | Informar vários valores de uma vez (a tela "Atualizar valores") |
| GET | `/investments/summary` | Totais por moeda, por classe, por conta; próximos vencimentos |

Permissões: `finances.investments.read`, `.create`, `.update`, `.delete`. O papel padrão já tem
`finances.*`, então ninguém perde acesso.

### Telas

Seguindo o `client-web/DESIGN.md`:

- **Página Investimentos** (`/finances/investments`, item novo no menu):
  - números: total por moeda, próximo vencimento;
  - alocação por classe e por conta;
  - posições abertas agrupadas por conta; cada linha mostra ativo, quantidade ou valor aplicado, custo
    médio, valor atual e sua data (desatualizado marcado);
  - botão principal **Atualizar valores**: uma tabela com todas as posições abertas, um campo de valor por
    linha, salvos de uma vez com uma data.
- **Página da conta**: aba **Posições**; o cabeçalho mostra dinheiro, posições e total.
- **Drawer da posição**: dados, movimentos, histórico de valores; ações: novo movimento, informar valor,
  desfazer o último movimento.
- **Formulários**: nova posição (conta, ativo escolhido ou criado ali, primeiro movimento), novo movimento
  (campos conforme o tipo), classes de ativo (página pequena, aberta pela página Investimentos).

### Fora da parte 1

Importação de qualquer fonte (parte 5, a próxima), cotações automáticas e curva (parte 2), conversão para
reais (parte 3), rentabilidade (B) e impostos (C), avisos de vencimento no Telegram, ferramentas do
Assistant.

### Testes

- Domínio: custo médio na compra, resultado na venda, resgate proporcional, desdobramento, come-cotas,
  desfazer restaurando o estado anterior, encerramento ao zerar.
- Handlers: o lançamento ligado e seu valor para cada tipo, a trava no ledger, moeda diferente, conta
  arquivada.
- Integração (Testcontainers): ida e volta posição → movimento → ledger → desfazer.
- Front: os formulários mandam o corpo certo por tipo; a tela Atualizar valores.

### Entrega

Quatro fatias, em ordem. Cada uma é construída e testada sem commit, revisada pelo usuário (no navegador
quando tem tela) e commitada só depois da aprovação.

| # | Fatia | Conteúdo | Pronta quando |
|---|---|---|---|
| I1 | **Domínio e schema** | Migrations `fin020`–`fin024` e a carga das classes do sistema; aggregates `AssetClass`, `Asset` e `Position` (a posição é dona dos seus movimentos e valores), as regras dos movimentos e o desfazer; repositórios e mapeamentos do EF | Testes de domínio de todas as regras passam; apply → rollback → apply roda no banco local |
| I2 | **Aplicação e API** | Commands e queries, o lançamento ligado por tipo, a trava no ledger, a exceção do INV-8, `positionsValue`/`total` no saldo da conta, `/investments/summary`, `finances.investments.*`, eventos de auditoria | Testes de handler e a ida e volta com Testcontainers passam |
| I3 | **Telas** | Página Investimentos, aba Posições e cabeçalho da conta, drawer da posição, formulários de posição e movimento, página de classes | Typecheck, lint e testes do front passam; conferido no navegador |
| I4 | **Atualizar valores e docs** | A tela Atualizar valores em lote; a especificação vai para os docs por tópico (`investments.md`, modelo de dados, referência da API, status de implementação) e sai deste plano | Conferido no navegador; este plano fica só com as partes 2 a 6 |

### Perguntas em aberto

- **Come-cotas e custo médio:** confirmar na regra oficial, antes da parte 4, se o imposto pago muda o
  custo de aquisição. A parte 1 só registra.
- **Fração do resgate parcial:** a regra proporcional usa o último valor informado; conferir contra um
  extrato real de banco durante os testes.
