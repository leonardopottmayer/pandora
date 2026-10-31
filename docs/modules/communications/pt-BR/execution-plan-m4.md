# Communications — Plano de Execução, M4 (Regras)

> **Status:** Em andamento: decisões aprovadas, passos 1–4 construídos. É a fase **M4** do [product-plan](product-plan.md), cuja §10 tem o modelo; o que existe hoje está no
> [Status de implementação](implementation-status.md).
> 🇺🇸 [English version](../en/execution-plan-m4.md)

**A M4 entrega:** regras numa tela só. No Gmail são filtros nativos, nos dois sentidos; no Titan e no IMAP rodam no motor do próprio Pandora. Sem condições com IA, sem "aplicar às mensagens existentes", sem ManageSieve (depois).

**Pronto quando** (§15): os filtros existentes do Gmail aparecem como regras ao conectar; uma regra criada no Pandora aparece como filtro no Gmail e é aplicada na chegada com o Pandora parado; um filtro editado no Gmail atualiza a regra no lugar; uma regra do Titan move uma mensagem nova em um ciclo.

**Regras agem sem ninguém olhando,** e mexem em filtros de que o usuário já depende. Isso molda as decisões abaixo.

---

## O que a documentação do Google diz

Conferido em 2026-10-31; muda o plano em três pontos.

| Fato | Consequência |
|---|---|
| `filters.list` aceita `gmail.modify`; `create` e `delete` aceitam **só** `gmail.settings.basic` | Ler filtros não pede novo consentimento. Escrever pede, então uma caixa sem o escopo mostra os filtros só para leitura e pede consentimento só quando precisa |
| "Only one user-defined label is allowed per filter" | Um "adicionar etiqueta" por regra do Gmail, checado ao salvar |
| Um filtro não tem `update`, e pode fazer `forward` e casar por `size`, `negatedQuery` e `excludeChats` | Editar é criar + apagar. Um filtro com o que o modelo não guarda não pode ser reescrito: veja abaixo |

Além disso: no máximo 1.000 filtros; `delete` é definitivo; `list` custa 1 unidade, `create` e `delete` 5. Lixeira (`addLabelIds: TRASH`) e marcar como lida (`removeLabelIds: UNREAD`) são ações listadas.

---

## Decisões fechadas

Aprovadas em 2026-10-31.

| Decisão | Valor | Motivo |
|---|---|---|
| Filtros que não cabem no modelo | **Importados só para leitura:** o filtro cru é guardado, e a tela o mostra com "edite no Gmail" | Reescrever um filtro que encaminha ou casa por tamanho perderia isso em silêncio. Rejeitado: deixá-los de fora, o que quebra "os filtros existentes aparecem" |
| Editar um filtro | **Criar o novo, depois apagar o antigo** | Na ordem contrária, o filtro se perde se a criação falhar. Pior caso aqui: dois por um instante |
| O que o Pandora apaga no Gmail | **Só quando o usuário apaga ou desativa aquela regra, com uma confirmação que diz isso.** Remover uma caixa do Pandora não apaga nenhum filtro | Um filtro pode ter levado anos para ficar certo |
| Desativar | Apaga o filtro do Gmail, mantém a regra (§10.3), porque o Gmail não tem filtro desativado. A confirmação diz que ele para de funcionar no Gmail também | |
| Como o motor recebe as mensagens (IMAP) | **Um subscriber de `MailMessageReceived`**, pelo outbox | Salvo na mesma transação da mensagem, então uma queda não perde a execução da regra, e uma regra lenta não segura a sincronização. Rejeitado: dentro do passo de sincronização, onde uma queda depois de salvar a perde |
| Email antigo | **Uma pasta que entra no escopo não pode publicar chegadas.** Corrigido primeiro, no passo 1 | Senão as regras rodariam em email antigo: movido ou na lixeira por uma regra que ninguém mirou nele. É o ponto em aberto da M2 |
| Várias regras casando | Todas valem, na ordem de `position`. Flags e etiquetas somam; **lixeira vence mover, e o primeiro mover vale** | A mensagem está numa pasta só. Dito de antemão para o resultado ser previsível |
| Regra vazia | **Recusada:** ao menos uma condição | Uma regra sem condição casa com todo email, e poderia mandá-lo para a lixeira |
| Regra que falha | Repetida pelo outbox; a sincronização segue. O erro aparece na regra | Uma regra ruim não pode parar a caixa |
| Etiqueta que sumiu | A regra vira `broken`, fica destacada e não roda | §10.1 |
| Falha ao sincronizar filtros | **Nunca falha o ciclo da caixa.** Vai para o log e aparece na tela de regras | Filtros são um trabalho à parte, e ler email importa mais |
| Condições no corpo (`has-words`) | Busca a mensagem crua uma vez por mensagem, só se alguma regra ainda precisar | Cada uma custa uma chamada ao provedor |
| Heurística de edição | §10.3: um sumiu + um novo no mesmo ciclo é edição, com `ponytail:` | Cobre editar um filtro por vez |
| Testes reais | Uma regra e uma etiqueta `pandora-test`, em mensagens enviadas para você mesmo; **nenhum filtro existente é editado ou apagado** | São seus, e nada aqui restaura um |

---

## Passos

Cada passo termina verde antes do próximo. Pontos de commit sugeridos: **M4a** = passo 1, **M4b** = passos 2–3, **M4c** = passos 4–6, **M4d** = passos 7–8. Commit só quando pedido.

### Passo 1 — Pasta que entra no escopo não gera chegada ✅

> **Construído (2026-10-31).** `RemoteChanges.ExistingIds`; o motor antigo publicou o evento falso no teste novo. Uma mensagem que cai numa pasta recém-criada no mesmo ciclo em que ela aparece conta como existente, então não gera chegada: é raro, e o erro é a favor de a regra não disparar. Testes: `SyncEngineTests` +1, `ImapMailboxProviderTests` +1.

- **Primeiro, um teste que falha:** uma pasta IMAP com mensagens aparece depois da carga inicial → as mensagens são salvas, nenhum `MailMessageReceived`. Uma mensagem nova de verdade nela num ciclo seguinte → um evento.
- O adaptador marca o que vem de uma pasta que o cursor não conhece; o motor não publica nada para essas.

### Passo 2 — O modelo ✅

> **Construído (2026-10-31).** Migration `20261031120000-create-table-com006-mail-rule`, `MailRule`, `RuleExecution`, `RuleStatus`, `RuleMatcher` (com `RulePlan`), `IMailRuleRepository`. Decidido aqui, além do texto abaixo: condição com valor vazio é recusada (casaria com todo email; `has-attachment` não leva valor); "adicionar etiqueta" é recusado fora do Gmail, onde a pasta é o lugar da mensagem; mover vence arquivar; `has-words` olha assunto e texto, como trecho, não palavra inteira; o motor nunca casa `provider-query`, que só o Gmail sabe rodar. `provider_filter_id` fica nulo com a regra desligada, porque o Gmail não tem filtro desligado. Testes: `MailRuleTests` e um ida e volta em `PersistenceTests`.

- Migration `com006_mail_rule`: caixa, nome, `position`, `conditions` e `actions` (jsonb), `execution` (`provider-filter`/`pandora`), `provider_filter_id`, `provider_filter_raw` (jsonb, para o que não cabe), `is_enabled`, `status` (`ok`/`broken`/`read-only`), `last_error`.
- `MailRule`: condições `from`, `to`, `subject`, `has-words`, `has-attachment`, `provider-query`; ações adicionar etiqueta, mover, arquivar, marcar como lida, estrela, lixeira. Validação: ao menos uma condição e uma ação; no Gmail, no máximo uma etiqueta do usuário; `provider-query` só no Gmail.
- `RuleMatcher`: puro, sobre a mensagem e, só quando preciso, o texto dela. Condições como o Gmail as trata, para uma regra se comportar igual nos dois: `from` e `to` como trecho, sem diferenciar maiúsculas; `to` cobre To e Cc.
- `RulePlan`: das regras que casam, um conjunto de ações, como decidido.
- **Testes (unitários):** cada condição; E lógico; a combinação; regras vazias recusadas; etiqueta além da permitida recusada.

### Passo 3 — O motor (IMAP) ✅

> **Construído (2026-10-31).** `RunMailRulesHandler`, e o `MailboxChangesStep` marca regras como `broken`. O que a checagem do outbox achou: a entrega é pelo menos uma vez, um handler que lança exceção é repetido até 8 vezes e depois vai para dead-letter, e uma queda depois de o handler terminar o repete. Por isso o handler pula o que já está feito, olhando a linha: mensagem que já está na pasta não é movida de novo. Decidido aqui: só `ProviderFailed` e `MailboxAuthFailed` lançam exceção para repetir; qualquer outra falha (sem pasta de arquivo, por exemplo) só se repetiria, então fica na regra. O erro vai para todas as regras que casaram, porque o plano as junta, e o próximo sucesso o limpa. Uma execução que falhou não é refeita depois de esgotadas as tentativas. Renomear pasta no IMAP é uma que some e outra que aparece, então as regras dela ficam `broken` e pedem a pasta de novo. Testes: `MailRulesEngineTests`.

- `RunMailRulesHandler : IIntegrationEventHandler<MailMessageReceived>`: só caixa com regras `pandora`; carrega; casa; aplica pelo mesmo caminho do `ApplyMessageAction`, sob a trava de ação.
- **Verificar como o outbox repete um handler que lança exceção, e se pode repetir um que deu certo.** Aplicar uma regra duas vezes precisa ser inofensivo: uma mensagem movida é achada pela linha, e lida e estrela são idempotentes.
- Uma regra cuja etiqueta sumiu → `broken` quando o passo de etiquetas a remove.
- **Testes:** uma mensagem nova é movida; email antigo não; uma regra falha → a sincronização segue e o erro fica na regra; `broken` não roda; uma regra entregue duas vezes age uma.

### Passo 4 — Filtros do Gmail no adaptador ✅

> **Construído (2026-10-31).** `ListFiltersAsync`, `CreateFilterAsync` e `DeleteFilterAsync` na porta (o IMAP responde `ActionNotSupported`), `GmailFilterMapper`, `MailErrors.MissingFilterScope`. Decidido aqui: no Gmail a regra aceita uma condição de cada tipo, porque o filtro tem um campo para cada, e `has-words` e `provider-query` dividem o `query`; um `query` com `:` volta como `provider-query`, senão como `has-words`, que o Gmail trata igual. Etiqueta do usuário é id que começa com `Label_`; qualquer outra etiqueta de sistema (`IMPORTANT`, `SPAM`, categoria) deixa o filtro só leitura. Apagar filtro que já não existe conta como feito. O escopo é checado no token, e o 403 do próprio Gmail dá o mesmo erro, porque os escopos registrados podem estar desatualizados. Sem checagem no Gmail real até o passo 8: tudo isso vem da documentação, inclusive o prefixo `Label_`. Testes: `GmailMailboxProviderTests` e `MailRuleTests`.

- `ListFiltersAsync`, `CreateFilterAsync`, `DeleteFilterAsync` na porta, só onde há `ServerFilters`.
- `GmailFilterMapper`: regra ⇄ filtro. Mover = adiciona etiqueta + remove `INBOX`; arquivar = remove `INBOX`; lida = remove `UNREAD`; estrela = adiciona `STARRED`; lixeira = adiciona `TRASH`. Um filtro com `forward`, `size`, `negatedQuery` ou qualquer coisa que não dê para expressar → só leitura.
- Criar ou apagar sem `gmail.settings.basic` → `MissingFilterScope`, que **não** para a caixa (não é `MissingScopes`).
- **Testes (respostas prontas):** cada um, nos dois sentidos; uma ida e volta não muda nada; um filtro que encaminha é só leitura; o erro de escopo.

### Passo 5 — Filtros nos dois sentidos

- `SyncFiltersCommand`, uma vez por ciclo, fora da trava de ação, falhas no log: `list`, diff por `provider_filter_id`, os casos da §10.3.
- Conectar uma caixa Gmail importa pelo mesmo diff.
- **Testes:** um filtro novo → uma regra; um sumiu → a regra sai (regra desativada é ignorada); um sumiu + um novo → mesma regra, mesmo nome e posição; dois sumiram + um novo → exclusão + importação; `list` falhando não mexe nas regras nem no ciclo.

### Passo 6 — API

- `GET/POST mailboxes/{id}/rules`, `PUT/DELETE rules/{id}`, `PUT mailboxes/{id}/rules/order`.
- Salvar no Gmail vai ao Gmail primeiro (C2). Editar é criar, salvar, apagar; se apagar falhar, a regra fica certa e o filtro antigo é informado.
- **Testes:** de outro usuário → 404; só leitura não edita; falha no Gmail não muda nada; uma edição cujo apagar falha mantém a nova e avisa; mais de 1.000 filtros é a recusa do Gmail, mostrada.

### Passo 7 — Web

- `mail/rules`: regras por caixa, arrastar para reordenar, botão de ativar, um selo de onde cada uma roda, `broken` e só leitura destacados; o formulário de condições e ações.
- Apagar ou desativar uma regra do Gmail avisa que muda o Gmail.
- Escrever sem o escopo: um "conceder acesso" que passa pelo consentimento, sem perder o formulário.
- i18n `mail.rules.*`.
- **Testes:** a validação do formulário; a confirmação; só leitura sem edição; o pedido de consentimento.

### Passo 8 — Verificação real e docs

Primeiro a sua parte no Google Cloud: declarar `gmail.settings.basic` na tela de consentimento e reconectar a conta do Gmail.

- **Importação:** seus filtros aparecem, nada muda no Gmail, e um com encaminhamento (se houver) fica só leitura.
- **Gmail:** uma regra `pandora-test` vira filtro; com o **backend parado**, uma mensagem enviada para você mesmo recebe a etiqueta. Editar no Gmail → a mesma regra atualiza. Desativar e apagar no Pandora → some do Gmail.
- **Titan:** uma regra `pandora-test` move uma mensagem nova em um ciclo; uma mensagem de antes não se move; apagar a pasta → `broken`.
- **Docs:** documentos de tópicos, en + pt-BR; o product plan fica só com o "depois"; uma nota **Construído** em cada passo.

---

## Limites conhecidos

- **Regras valem para email que chega depois de salvas** (§10.1).
- **Os filtros do webmail do Titan não são vistos,** e ele não tem ManageSieve: lá, as regras só rodam com o Pandora ligado.
- **Atraso no IMAP:** até um ciclo, 2 minutos.
- **A heurística de edição** pode ler duas edições num ciclo como exclusão + importação, perdendo o nome.
- **Uma regra `has-words` numa caixa movimentada** custa uma chamada ao provedor por mensagem que chegar até ela.
