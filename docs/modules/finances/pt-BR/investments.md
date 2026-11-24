# Investimentos

[← Voltar ao índice](README.md) · Relacionados: [Contas](accounts.md), [Lançamentos](transactions.md), [Modelo de dados](data-model.md#fin020_system_asset_class), [Plano de produto](product-plan.md)

> **Status:** a parte 1, o núcleo da carteira, está construída (2026-11-23): posições, movimentos, valores informados e as telas. Cotações, importação, conversão entre moedas, rentabilidade e impostos são as próximas partes, no [Plano de produto](product-plan.md).

## Contexto de negócio

Saber **quanto tenho, onde e em quê**, com valor atualizado, em todos os bancos, corretoras e países:
renda fixa (CDB, LCI, LCA, Tesouro…), ações, FIIs, fundos, ETFs no exterior (Avenue/Nasdaq), moedas
(USD, EUR) e cripto. Flexível para aceitar qualquer classe usada hoje ou no futuro.

## Decisões de design

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
| INV-9 | **O valor atual é calculado na leitura, como o saldo de uma conta (D1).** O último valor informado é a âncora; os movimentos registrados depois dela a ajustam: uma compra soma o que foi pago, uma venda ou come-cotas mantém a fração das unidades que ficaram, um resgate tira o valor bruto. Nada derivado é gravado. | Achado na conferência das telas: um resgate deixava o valor como estava antes de o dinheiro sair. Gravar um valor por movimento (a alternativa) teria de ser mantido em sincronia a cada desfazer. |
| INV-10 | **Na página da conta, as posições são uma seção acima do extrato**, não uma aba: a página não tem abas. Aparece para contas com posições e para toda conta `investment`. | A especificação dizia aba; a página onde ela entra é uma coluna só. |

## Conceitos

| Termo | Significado |
|---|---|
| **Classe de ativo** | Que tipo de investimento (CDB, ação, FII…). Classes do sistema, semeadas (`fin020`), mais as do usuário (`fin025`), separadas como as categorias do sistema e do usuário; um ativo tem exatamente uma das duas. Cada uma tem um **comportamento**: `unit` (quantidade e custo médio) ou `application` (renda fixa: data, taxa, vencimento). |
| **Ativo** | Algo que o usuário tem: nome, código opcional (ticker ou ISIN), emissor opcional, moeda, classe. |
| **Posição** | Um ativo dentro de uma conta. Ativos `unit`: uma posição aberta por ativo por conta. Ativos `application`: uma posição por aplicação. Aberta enquanto tiver algo, encerrada quando zera. |
| **Movimento** | Algo que aconteceu com uma posição: abertura, compra, venda, resgate, vencimento, provento, desdobramento, come-cotas. Guarda data, quantidade, preço unitário, valor bruto, custos e imposto retido. |
| **Valor informado** | Quanto uma posição vale numa data, com a origem. O mais recente é o valor atual. |

Regras que valem sempre:

- A moeda do ativo tem que ser a moeda da conta (a conversão é a parte 3).
- Conta arquivada não recebe posições nem movimentos novos; conta com posições não pode ser excluída.
- Toda escrita grava um evento de auditoria (`fin016`), como no resto do módulo.

As tabelas são `fin020`–`fin025`, descritas no [Modelo de dados](data-model.md#fin020_system_asset_class).

## Regras dos movimentos

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

## Valor e totais

- **Valor atual** de uma posição = seu último valor informado. Sem nenhum: o custo (`q·c`, ou `v`),
  mostrado como "sem valor informado".
- Um valor com mais de **30 dias** aparece como desatualizado.
- **Total da conta** = saldo em dinheiro do ledger + valor atual das posições abertas.
  `GET /accounts/{id}/balance` ganha `positionsValue` e `total`; os campos existentes mantêm o sentido.
- Os totais são **por moeda**; nada é convertido (parte 3).
- O **valor atual** segue o INV-9. A data mostrada ao lado é a da âncora; o valor já inclui os movimentos registrados depois dela.

## Telas

Seguindo o `client-web/DESIGN.md`:

- **Página Investimentos** (`/finances/investments`, item novo no menu):
  - números: total por moeda, próximo vencimento;
  - alocação por classe e por conta;
  - posições abertas agrupadas por conta; cada linha mostra ativo, quantidade ou valor aplicado, custo
    médio, valor atual e sua data (desatualizado marcado);
  - botão principal **Atualizar valores**: uma tabela com todas as posições abertas, um campo de valor por
    linha, salvos de uma vez com uma data.
- **Página da conta**: seção **Posições** acima do extrato (INV-10); o número *Total* mostra dinheiro mais posições.
- **Drawer da posição**: dados, movimentos, histórico de valores; ações: novo movimento, informar valor,
  desfazer o último movimento.
- **Formulários**: nova posição (conta, ativo escolhido ou criado ali, primeiro movimento), novo movimento
  (campos conforme o tipo), classes de ativo (página pequena, aberta pela página Investimentos).
- **Atualizar valores** (botão principal da página Investimentos): todas as posições abertas numa lista com um campo de valor cada, uma data para todas; só as linhas preenchidas são enviadas (`POST /positions/valuations`). A lista só é montada enquanto aberta, então começa vazia.

## API (`/api/v{n}/finances`)

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

## Eventos de auditoria

| Entidade | Eventos |
|---|---|
| `asset-class` | `asset-class.created`, `.renamed`, `.archived`, `.unarchived` |
| `asset` | `asset.created`, `asset.updated` |
| `position` | `position.created`, `.movement-added`, `.movement-undone`, `.deleted`, `.valued` |
| `transaction` | `transaction.created` / `transaction.voided` dos lançamentos que os movimentos criam e desfazem, com `positionMovementId` no payload |

## Fora da parte 1

Importação de qualquer fonte (parte 5, a próxima), cotações automáticas e curva (parte 2), conversão para
reais (parte 3), rentabilidade (B) e impostos (C), avisos de vencimento no Telegram, ferramentas do
Assistant.

## Perguntas em aberto

- **Come-cotas e custo médio:** confirmar na regra oficial, antes da parte 4, se o imposto pago muda o
  custo de aquisição. A parte 1 só registra.
- **Fração do resgate parcial:** a regra proporcional usa o valor atual da posição (INV-9); conferir
  contra um extrato real de banco durante o uso.
