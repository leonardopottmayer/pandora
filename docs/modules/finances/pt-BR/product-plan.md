# Finances — Plano de produto (trabalho restante)

> **Status:** a parte 1 dos investimentos (o núcleo da carteira) está **construída** (2026-11-23) e documentada em [Investimentos](investments.md). O que falta são as partes 2 a 6 abaixo.
> 🇺🇸 [English version](../en/product-plan.md)

---

## Investimentos

### Objetivo

Saber **quanto tenho, onde e em quê**, com valor atualizado, em todos os bancos, corretoras e países:
renda fixa (CDB, LCI, LCA, Tesouro…), ações, FIIs, fundos, ETFs no exterior (Avenue/Nasdaq), moedas
(USD, EUR) e cripto. Flexível para aceitar qualquer classe usada hoje ou no futuro.

### As partes e a ordem

| # | Parte | O que resolve | Depende de |
|---|---|---|---|
| 1 | **Núcleo da carteira** — construído, ver [Investimentos](investments.md) | Ativos, posições por conta, movimentos (compra, venda, aporte, resgate, provento, come-cotas, desdobramento), custo médio, valores informados | — |
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

### Perguntas em aberto

- **Come-cotas e custo médio:** confirmar na regra oficial, antes da parte 4, se o imposto pago muda o
  custo de aquisição. A parte 1 só registra.
- **Fração do resgate parcial:** a regra proporcional usa o valor atual da posição (INV-9); conferir
  contra um extrato real de banco durante o uso.
