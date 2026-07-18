# Visão geral — Negócio e princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

O módulo **Finances** é um gerenciador completo de finanças pessoais dentro do monolito modular
Pandora. Ele permite a um único usuário:

- Cadastrar **contas** (dinheiro, corrente, poupança, internacional, crypto, investimento, outra) e
  **cartões de crédito** com ciclo completo de **fatura**.
- Registrar **lançamentos** direto em conta ou em fatura de cartão: entrada, saída, transferência,
  aporte/resgate de investimento, rentabilidade, pagamento de fatura, estorno, ajuste e quitação de
  fatura sem caixa.
- Classificar lançamentos com **categorias do sistema** (seed, hierárquicas) *e* **categorias do
  usuário** (custom, hierárquicas) — duas dimensões independentes, ambas usáveis no mesmo lançamento.
- Anexar **tags** livres a qualquer entidade.
- Definir **transações recorrentes** que geram sugestões automaticamente.
- **Importar arquivos bancários** (OFX e CSV, com layouts por banco) para contas e cartões.
- Revisar tudo que é automático em um único **inbox** de *transações pendentes* que o usuário edita,
  aprova, rejeita ou vincula a um lançamento existente.
- Desfazer erros com segurança: **cancelar / desfazer cancelamento / estornar**, nunca um hard delete
  de dinheiro postado.
- Confiar em uma **trilha de auditoria e proveniência** completa para toda mudança relevante.

## 2. Princípios centrais

1. **O ledger é a fonte da verdade.** Saldo nunca é campo editável — é o somatório com sinal dos
   lançamentos `posted`. Nada "ajusta saldo na mão"; correções são elas mesmas lançamentos
   (`adjustment`), auditáveis como qualquer outro. *(Decisão de design D1.)*
2. **Nada automático entra no ledger sem rastro.** Importação e recorrência passam por staging (ou
   são auto-postadas com flag explícita do usuário), e a cadeia de proveniência é *estrutural*
   (chaves estrangeiras), não só um log textual. *(D4.)*
3. **Append-only para dinheiro.** Tudo que já foi `posted` permanece no ledger para sempre.
   `void`/`unvoid` mudam o status de uma linha; `reverse` adiciona uma linha-espelho nova. Eventos de
   auditoria nunca são alterados ou apagados.
4. **O modelo é feito para evoluir** (orçamentos, metas, split de transação, open finance) sem
   reescrever o core.

## 3. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Conta** (Account) | Repositório de saldo do usuário: dinheiro/carteira, corrente, poupança, internacional, crypto, investimento, outra. Tem moeda fixa. |
| **Cartão** (Card) | Cartão de crédito. **Não é conta**: gastos vão para a fatura; só o pagamento da fatura movimenta uma conta. Cartão de débito não é modelado — débito é lançamento direto na conta. |
| **Fatura** (Statement) | Ciclo mensal de um cartão: agrupa transações entre fechamentos; tem data de fechamento, vencimento e status (aberta → fechada → paga/parcialmente-paga/vencida). |
| **Lançamento** (Transaction) | Movimento atômico no ledger. Afeta exatamente **um** destino: uma conta **ou** uma fatura. Transferência = dois lançamentos ligados. |
| **Grupo de transferência** | Par de transações (`transfer-out` na origem + `transfer-in` no destino) ligadas por um id comum. Suporta moedas diferentes (com taxa de câmbio registrada). |
| **Parcelamento** (Installment plan) | Compra no cartão dividida em N parcelas: um plano + N transações, uma por fatura. |
| **Categoria do sistema** | Categoria mantida pelo sistema, seed, hierárquica (pai → filho), tipada por natureza (despesa/receita). Global, igual para todos os usuários. |
| **Categoria do usuário** | Categoria criada pelo usuário, também hierárquica. Coluna separada: um lançamento pode ter categoria do sistema **e** do usuário ao mesmo tempo. |
| **Tag** | Rótulo livre do usuário, aplicável a qualquer entidade do módulo por vínculo polimórfico. |
| **Transação recorrente** (Recurrence) | Template de lançamento + regra de repetição. Gera transações pendentes (ou posta direto, se `auto_post`). |
| **Transação pendente** (sugestão) | Registro de staging: proposta de lançamento vinda de importação ou recorrência. Editável; aprovação cria a transação real; rejeição encerra. Guarda snapshot imutável da sugestão original. |
| **Arquivo / linha de importação** | Arquivo bancário enviado (OFX/CSV) e cada linha/registro extraído dele, com o dado bruto preservado. |
| **Layout de importação** | Perfil de parsing para quirks de OFX ou estrutura de CSV por banco: mapeamento de colunas, formato de data, separador decimal, convenção de sinal, padrões de detecção de parcela. |
| **Conciliação** | Casamento de uma linha importada com um lançamento já existente ou *esperado* (agendado, gerado por recorrência). Aprovar concilia: confirma/posta o existente com os valores reais, sem duplicar. |
| **Evento de auditoria** | Registro append-only de qualquer mudança relevante: quem, quando, em quê, o que mudou (diff), com um correlation id. |

## 4. Escopo

### No escopo (implementado — ver [Status de implementação](implementation-status.md))

Contas, cartões e faturas (com quitação/write-off de onboarding e reabertura), o ledger completo,
transferências, parcelamento (manual e inferido de importação com projeções), recorrências + inbox,
importação OFX e CSV com auto-detecção de layout, dedup/conciliação, tags, categorias de sistema e do
usuário, reversibilidade (cancelar/desfazer/estornar + proteções de exclusão) e a trilha de auditoria.

### Fora de escopo / futuro

| Feature | Status |
|---|---|
| **Regras de categorização** (auto-categorizar importações, `fin015`) | Projetado, ainda não implementado. |
| **Relatórios** (fluxo de caixa, por categoria, histórico de saldo, agenda) | Ainda não implementado (só existe a timeline de auditoria). |
| **Eventos de integração com Notifications** (fatura fechada/a vencer/vencida, importação concluída) | Projeto Contracts existe mas está vazio. |
| **Split de transação** (um lançamento, várias categorias) | Futuro — tabela filha aditiva. |
| **Orçamentos / metas** | Futuro. |
| **Anexos / comprovantes** | Futuro. |
| **Snapshots de saldo** (performance com histórico grande) | Futuro — o ledger continua a verdade. |
| **Open finance / sincronização automática** (Pluggy etc.) | Futuro — entra pelo mesmo pipeline de import. |
| **Consolidação multi-moeda** (totais convertidos) | Futuro — precisa de provedor de câmbio. |
| **Multiusuário / household compartilhado** | Futuro — o `user_id` em tudo deixa a porta aberta. |
