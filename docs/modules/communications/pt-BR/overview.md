# Visão geral — Fronteira e princípios

[← Voltar ao índice](README.md) · Relacionados: [Arquitetura](architecture.md), [Modelo de dados](data-model.md)

---

## 1. O que o módulo faz

O **Communications** é a **caixa de entrada** do usuário dentro do Pandora: as mensagens que chegam
*para* o usuário, de qualquer provedor, num lugar só. Email vem primeiro.

Construído hoje (M1, conectar e ler; M2, organizar):

- Conectar **quantas caixas o usuário quiser**: Gmail pela Gmail API, Titan Email e qualquer outro
  servidor por IMAP.
- **Espelhar** os cabeçalhos e as etiquetas de cada caixa, do mais novo para o mais antigo, e mantê-los
  atualizados com um job em segundo plano (a cada 2 minutos) ou com "Sincronizar agora".
- **Ler** as mensagens numa lista só, filtrada por caixa, etiqueta, não lidas ou texto. O corpo é buscado
  quando a mensagem é aberta e depois fica em cache.
- **Mostrar o email com segurança**: o HTML é limpo e mostrado num iframe em sandbox, e as imagens externas
  ficam bloqueadas até o usuário pedir.
- **Organizar**: lida e não lida, estrela, mover, arquivar e lixeira, em lote, e etiquetas criadas, renomeadas e apagadas. Tudo vai primeiro ao
  provedor, e o que muda no webmail volta para o Pandora.
- **Publicar `MailMessageReceived`** pelo outbox a cada mensagem nova, para outros módulos processarem
  email. Nada consome esse evento ainda.

Enviar (M3) e regras (M4) são planos: veja o [plano de produto](product-plan.md).

## 2. O que ele não é

- **Não é notificação de saída.** O Pandora avisando o usuário (Telegram, email do sistema) é o
  [Channels](../../channels/pt-BR/README.md). O Communications é o outro sentido: as caixas do próprio usuário.
- **Não é servidor de email nem arquivo.** Quem guarda o email é o provedor. Remover uma caixa do Pandora
  apaga a cópia do Pandora e nada no provedor.
- **Não guarda credenciais.** Senhas e tokens OAuth ficam no
  [Integrations](../../integrations/pt-BR/README.md); a caixa guarda só o id da conta.
- **Não é um jeito de perder email.** Nada aqui apaga uma mensagem de vez: "excluir" é a lixeira do provedor, e só uma pasta IMAP vazia pode ser
  apagada, porque o que está dentro iria junto.

## 3. Princípios

1. **O provedor é a fonte da verdade.** O espelho local pode ser refeito sincronizando de novo, e é isso que
   acontece quando um cursor de sincronização expira.
2. **Um modelo neutro: mensagens + etiquetas.** Uma etiqueta do Gmail e uma pasta IMAP são ambas uma
   **etiqueta**. Pasta é uma etiqueta marcada como **exclusiva**: a mensagem tem no máximo uma.
3. **O provedor declara o que sabe fazer.** Tudo acima do `IMailboxProvider` vê as mesmas operações e cursores
   opacos, seja qual for o provedor.
4. **Cabeçalhos espelhados, corpo sob demanda.** O corpo é buscado ao abrir e fica em cache; anexos nunca são
   guardados.
5. **Conteúdo externo bloqueado por padrão.** Um pixel de rastreamento nunca dispara só porque a mensagem foi
   aberta.
6. **O provedor primeiro.** Uma ação do usuário vai ao provedor, e a linha só muda depois que ele aceita. Uma falha não muda nada.
7. **Instância única.** Cada caixa tem duas travas em processo, a mesma premissa da trava de refresh do Integrations.

## 4. Linguagem ubíqua (glossário)

| Termo | Significado |
|---|---|
| **Caixa** (*mailbox*) | Uma conta de email conectada (`com001`), rodando sobre uma conta do Integrations. |
| **Provedor** | Como a caixa é acessada: `gmail`, `titan` (IMAP com os servidores do Titan já preenchidos) ou `imap`. |
| **Etiqueta** (*label*) | Uma etiqueta do Gmail ou uma pasta IMAP (`com002`). |
| **Etiqueta exclusiva** | Etiqueta da qual a mensagem só pode ter uma: uma pasta IMAP. |
| **Papel** (*role*) | Para que a etiqueta serve: `inbox`, `sent`, `drafts`, `trash`, `spam`, `archive`, ou nenhum. |
| **Carga inicial** (*backfill*) | O primeiro download dos cabeçalhos da caixa, do mais novo para o mais antigo, em lotes que podem ser retomados. |
| **Ciclo** | Uma sincronização de uma caixa: etiquetas, depois mudanças, depois lotes da carga inicial enquanto houver orçamento. |
| **Cursor de sincronização** | Onde a sincronização incremental parou: um `historyId` do Gmail ou o estado IMAP por pasta. Opaco fora do adaptador. |
| **Snapshot** | Todos os ids de mensagem que uma etiqueta tem agora; no IMAP, é como as remoções são detectadas. |
| **Chave da conversa** (*thread key*) | O que agrupa uma conversa: o `threadId` do Gmail, ou no IMAP o primeiro Message-ID de `References`. |
| **Cache de conteúdo** | Corpos abertos (`com005`), apagados quando não são abertos por `ContentCacheRetentionDays`. |

## 5. Escopo

### Dentro do escopo (implementado — veja [Status de implementação](implementation-status.md))

O schema `communications` (`com001`–`com005` + outbox); os adaptadores Gmail e IMAP; carga inicial e
sincronização incremental; o cache de conteúdo e os anexos; os endpoints de caixas, etiquetas e mensagens; etiquetas nos dois sentidos e ações em
lote; `MailMessageReceived`; as telas `mail` e `mail/accounts`.

### Fora do escopo / futuro (veja [product-plan.md](product-plan.md))

| Funcionalidade | Fase |
|---|---|
| Escrever, responder, encaminhar, anexos | M3 |
| Regras, com filtros do Gmail nos dois sentidos | M4 |
| Push do Gmail no lugar do polling, Radar consumindo o evento, mais provedores | Depois |
