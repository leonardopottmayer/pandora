# Módulo Communications — Plano de Produto

> **Status:** M1 e M2 prontas (2026-10-31): o que existe está no [Status de implementação](implementation-status.md) e
> nos documentos de tópicos, então este plano guarda só o que falta, M3–M4. Cobre **email**, a primeira capacidade.
> Como foram construídas: [M1](execution-plan.md), [M2](execution-plan-m2.md).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Integrations](../../integrations/pt-BR/README.md) · [Channels](../../channels/pt-BR/README.md) ·
> [Arquitetura de mensageria](../../../architecture/pt-BR/messaging.md)

---

## 1. O que o módulo faz

**Communications** é a **caixa de entrada** do usuário dentro do Pandora: as mensagens que chegam
*para* o usuário, de qualquer provedor, num lugar só. Email vem primeiro.

- Conectar **quantas caixas quiser**, de provedores diferentes. Hoje: **Gmail** e **Titan Email**
  (A2 Hosting). Qualquer servidor IMAP/SMTP funciona pelo provedor genérico; provedores novos
  (Outlook via Graph, por exemplo) são mais um adaptador.
- **Ler** todas as caixas numa lista só, filtrando por conta, etiqueta, não lida ou texto.
- **Organizar**: etiquetas e pastas, mover, arquivar, lixeira, lida/não lida, estrela — em lote.
  O que muda no Pandora aparece no provedor, e o que muda no provedor aparece no Pandora.
- **Enviar**: escrever, responder, encaminhar, anexos.
- **Regras** ("de X → etiqueta Y, pular a caixa de entrada"), guardadas no Pandora e editadas numa
  tela só. No Gmail viram **filtros nativos**, então o Gmail aplica na chegada mesmo com o Pandora
  fora do ar; edições feitas no Gmail voltam para o Pandora. Onde o provedor não tem API de filtros, o
  próprio Pandora aplica as regras.
- Publicar `MailMessageReceived` para cada mensagem nova, para outros módulos processarem email. O
  primeiro consumidor previsto é o **Radar** (um digest curado de tecnologia que lê newsletters).

### O que ele não é

- **Não é notificação de saída.** O Pandora avisando o usuário de algo (Telegram, email do sistema)
  é o [Channels](../../channels/pt-BR/README.md). O Communications é o outro sentido: as caixas do
  próprio usuário.
- **Não é servidor de email nem arquivo.** O provedor guarda os emails. O Pandora espelha metadados
  e faz cache de corpos; apagar uma caixa no Pandora não apaga nada no provedor.
- **Não é cofre de credenciais.** Senhas e tokens OAuth ficam no
  [Integrations](../../integrations/pt-BR/README.md).

---

## 2. Nomes e coordenadas

| Item | Valor |
|---|---|
| Projetos do backend | `Pottmayer.Pandora.Modules.Communications.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `communications` |
| Prefixo de tabela | `comXXX_`, PK `uuid_generate_v7()` |
| Seção de options | `Pandora:Communications` |
| Base da API | `/api/v{version}/communications` — JWT, escopo de usuário de sempre |
| Frontend | `client-web/src/modules/communications`, rotas `mail`, `mail/accounts`, `mail/rules` |
| Migrations | `migrations/migrations/communications/` |
| Bibliotecas | Gmail REST pelo `HttpClient`, MailKit + MimeKit (IMAP/SMTP e montagem do MIME para os dois) |

---

## 3. Princípios

1. **O provedor é a fonte da verdade dos emails.** O espelho local (metadados, etiquetas, corpos em
   cache) pode ser reconstruído sincronizando de novo. *(C1)*
2. **Grava primeiro no provedor.** Uma ação do usuário (etiqueta, mover, lixeira, enviar) vai
   primeiro ao provedor; o espelho local só muda depois que o provedor aceita. Se o provedor falhar, o
   usuário vê o erro e nada muda localmente. Sem fila otimista. *(C2)*
3. **Um modelo neutro: mensagens + etiquetas.** Um marcador do Gmail e uma pasta IMAP são ambos uma
   **etiqueta**; pasta é uma etiqueta marcada como **exclusiva** (a mensagem tem no máximo uma).
   "Mover" não é operação própria — é "trocar a etiqueta exclusiva", e cada adaptador traduz. *(C3)*
4. **O provedor declara capacidades, e tudo se adapta.** As telas e o motor de regras leem as
   capacidades do provedor em vez de testar o nome dele. *(C4)*
5. **Cabeçalhos espelhados, corpos sob demanda.** Os metadados de toda mensagem são espelhados; o
   corpo é buscado ao abrir (ou quando uma regra precisa) e vai para o cache; anexos passam direto, não
   são guardados. *(C5)*
6. **Credenciais ficam no Integrations.** O Communications guarda host/porta (uso); usuário,
   senha e tokens OAuth são do Integrations (credencial). *(C6)*
7. **Conteúdo remoto bloqueado por padrão.** O HTML do email renderiza isolado, sem scripts e sem
   imagens remotas até o usuário pedir — pixel de rastreamento nunca dispara só de abrir a mensagem.
   *(C7)*
8. **Instância única.** Cada caixa tem duas travas em processo, a mesma premissa da trava de refresh do Integrations: veja a
   [Arquitetura](architecture.md). Rever só se o host escalar. *(C8)*

---

## 4. Provedores

### 4.1 A porta do provedor

`IMailboxProvider` (Domain/Ports), uma implementação por tipo de provedor:

| Operação | O que faz |
|---|---|
| `GetCapabilities` | `MultipleLabels`, `ServerFilters`, `NativeThreads` |
| `Sync(cursor)` | mudanças desde o cursor (adicionadas, removidas, etiquetas/flags alteradas) + o novo cursor |
| `ListLabels` / `CreateLabel` / `RenameLabel` / `DeleteLabel` | gestão de etiquetas |
| `ApplyLabelChanges(ids, add, remove)` | etiquetas, mover, arquivar, lida/não lida (`UNREAD`), estrela |
| `Trash(ids)` | para a lixeira do provedor |
| `GetContent(id)` / `GetAttachment(id, attId)` | corpo + lista de anexos; os bytes de um anexo |
| `Send(MimeMessage, threadHint)` | enviar; o adaptador salva a cópia enviada onde o provedor não salva |
| `ListFilters` / `CreateFilter` / `DeleteFilter` | só quando `ServerFilters` |

### 4.2 Adaptadores

| Adaptador | Provedores | Como |
|---|---|---|
| `GmailMailboxProvider` | `gmail` | Gmail API com o access token do `IExternalCredentialProvider`. `history.list` para sincronização incremental, marcadores e conversas nativos, envio com o MIME cru, `settings.filters` para as regras. |
| `ImapMailboxProvider` | `titan`, `imap` | MailKit: IMAP com UID + CONDSTORE (QRESYNC quando houver), SMTP para enviar. `titan` é uma predefinição do `imap` (`imap.titan.email:993`, `smtp.titan.email:465`). |

### 4.3 Como o modelo de etiquetas se traduz

| | Gmail | IMAP (Titan, genérico) |
|---|---|---|
| Etiqueta | um marcador do Gmail, id estável | uma pasta, identificada pelo caminho |
| `is_exclusive` | falso (a mensagem tem várias) | verdadeiro (a mensagem está numa pasta) |
| Caixa de entrada / arquivo | marcador `INBOX` / sem `INBOX` | pasta `INBOX` / a pasta com papel `archive` |
| Mover para L | adicionar L, tirar `INBOX` (ou a exclusiva atual) | `MOVE` para L |
| Lida / estrela | os marcadores `UNREAD` / `STARRED` | as flags `\Seen` / `\Flagged` |
| Conversa | `threadId` | o primeiro Message-ID de `References` (ou o da própria mensagem) |

---

## 5. Credenciais

Construído na M1: veja o [Integrations](../../integrations/pt-BR/README.md) e a [Arquitetura](architecture.md). Falta:

- A M3 precisa de `gmail.send` e a M4 de `gmail.settings.basic`. A M1 só pede `gmail.modify`, então as contas já
  conectadas terão de consentir de novo; a tela de consentimento também precisa declarar os escopos novos.
- **Google Cloud:** em **Testing** o refresh token expira a cada 7 dias. Ainda falta confirmar se passar o projeto
  para **In production** sem verificação basta para uso pessoal (o
  [guia de configuração](../../../deployment/google-and-gemini-setup.md) assume que a verificação é exigida).

---

## 6. Modelo de dados

O que existe está no [Modelo de dados](data-model.md). Falta:

| Tabela | Guarda |
|---|---|
| `com006_mail_rule` | caixa, nome, `position`, `conditions` e `actions` (jsonb), `execution` (`provider-filter`/`pandora`), `provider_filter_id`, `is_enabled`, `status` (`ok`/`broken`) |

---

## 7. Sincronização

Construída na M1: veja a [Arquitetura](architecture.md#3-o-ciclo-de-sincronização). Da M2 em diante, a comparação de
etiquetas tem uma lacuna conhecida: uma pasta IMAP não tem id, então renomear no webmail aparece como "pasta sumiu +
pasta nova" e as mensagens dela são lidas de novo.

---

## 8. Organizar

Construído na M2: veja a [Arquitetura](architecture.md#5-organizar).

---

## 9. Envio

- **Escrever**: de (qual caixa), para, cc, cco, assunto, corpo HTML, anexos (enviados na mesma
  requisição multipart, dentro do limite do provedor — 25 MB no Gmail). O editor reaproveita o do
  Notes se ele gerar um HTML que sirva; senão, escolhe-se outro na M3.
- **Responder / encaminhar** preenchem `In-Reply-To` e `References`; no Gmail o `threadId` também
  vai, para a mensagem cair na mesma conversa.
- **Síncrono**: "Enviado" ou o erro na tela; na falha o formulário mantém o conteúdo. Sem fila, sem
  desfazer envio.
- **Cópia em Enviados**: o Gmail salva sozinho. Via SMTP, o adaptador faz `APPEND` na pasta com papel
  `sent` — a menos que a descrição do provedor diga que o servidor já salva (a verificar no Titan,
  §14).
- **Rascunhos** não são sincronizados com o provedor: o formulário guarda o conteúdo no navegador.

---

## 10. Regras

### 10.1 Modelo

- Todas as condições precisam bater (E). Para OU, duas regras.
- **Condições:** `from`, `to`, `subject`, `has-words`, `has-attachment`, `provider-query` (a busca
  crua do Gmail, para filtros importados não perderem nada; só caixas Gmail).
- **Ações:** adicionar etiqueta, mover (trocar a etiqueta exclusiva), arquivar, marcar como lida,
  estrela, lixeira.
- Todas as regras que batem são aplicadas, na ordem de `position` — igual ao Gmail.
- Uma regra que referencia uma etiqueta que não existe mais fica `broken` e aparece em destaque.
- As regras valem para mensagens que chegam depois de salvas; "aplicar às mensagens existentes" fica
  para depois.

### 10.2 Onde a regra roda

Decidido ao salvar a regra:

- **Caixa Gmail → `provider-filter`.** Tudo o que o modelo da v1 oferece é expressável como filtro do
  Gmail, então toda regra Gmail é filtro nativo por enquanto.
- **Senão → `pandora`.** O motor de regras roda dentro do job de sincronização, logo depois de salvar
  as mensagens novas. Uma regra com `has-words` busca o corpo (que fica no cache). Condições futuras
  que o Gmail não expressa (classificação com IA) também rodam aqui, em qualquer provedor.

### 10.3 Filtros do Gmail, nos dois sentidos

Para regras `provider-filter`, **o Gmail é a fonte da verdade**; o Pandora espelha e edita. Só nome,
posição e `is_enabled` são do próprio Pandora.

- **Pandora → Gmail.** Criar → `CreateFilter`. Editar → apagar + criar (a API do Gmail não tem
  update) e guardar o novo `provider_filter_id`. Apagar → `DeleteFilter`. **Desligar** → apaga o
  filtro no Gmail mas mantém a regra com `provider_filter_id` nulo (o Gmail não tem filtro desligado);
  ligar recria.
- **Gmail → Pandora.** Toda sincronização chama `filters.list` (uma chamada barata) e compara pelo
  `provider_filter_id`:
  - filtro desconhecido pelo Pandora → regra nova (nome gerado a partir dos critérios);
  - filtro conhecido que sumiu → a regra é apagada (regras desligadas ficam fora da comparação);
  - uma edição no Gmail chega como "um sumiu + um novo" (o Gmail recria filtros). Quando exatamente
    um some e exatamente um aparece no mesmo ciclo, trata-se como edição e mantém nome e posição;
    senão, apagar + importar. *(heurística, marcada `ponytail:` no código — cobre editar um filtro por
    vez)*
- Conectar uma caixa Gmail importa os filtros existentes pela mesma comparação.

### 10.4 Titan

Os filtros do webmail do Titan não têm API conhecida, então as regras do Titan rodam só no motor do
Pandora, e filtros criados no webmail não são vistos. Se o Titan oferecer **ManageSieve** (§14), um
`provider-filter` via Sieve vira uma fase posterior.

---

## 11. Telas

- **`mail` — a caixa, três colunas.** Esquerda: caixas e suas etiquetas com contagem de não lidas
  (clique direito para criar/renomear/apagar etiqueta). Centro: a lista — virtualizada, seleção
  múltipla, ações em lote, filtro por conta, busca. Direita: o painel de leitura, com a visão de
  conversa.
  - A busca na v1 é local: remetente, assunto, prévia. Buscar no corpo pelo provedor fica para
    depois.
  - O HTML renderiza num `iframe` com `sandbox` (sem scripts). Imagens remotas ficam bloqueadas até
    "Mostrar imagens"; imagens `cid:` embutidas vêm pelo endpoint de anexo.
- **Escrever** — uma gaveta, usada também para responder e encaminhar.
- **`mail/accounts`** — conectar Gmail (usa a conexão Google; pede reconexão se faltarem os escopos
  do Gmail); adicionar Titan/IMAP (endereço, senha, servidor já preenchido no Titan — salva a
  credencial no Integrations e depois cria a caixa); status, progresso da carga inicial,
  "Sincronizar agora".
- **`mail/rules`** — regras por caixa: arrastar para reordenar, liga/desliga, selo de onde roda
  (Gmail / Pandora), `broken` em destaque; formulário de condições/ações.

---

## 12. API (`/api/v1/communications`)

O que existe está na [Referência da API](api-reference.md). Falta:

| Recurso | Endpoints |
|---|---|
| Envio | `POST messages/send` (multipart; `replyTo` / `forwardOf` opcionais) |
| Regras | `GET/POST mailboxes/{id}/rules`, `PUT/DELETE rules/{id}`, `PUT mailboxes/{id}/rules/order` |

---

## 13. Erros

As falhas de sincronização estão na [Arquitetura](architecture.md#falhas). Falta, para as ações do usuário:

| Falha | Tratamento |
|---|---|
| Envio falha no provedor | o erro volta para a tela, nada muda localmente; o formulário mantém o conteúdo |

---

## 14. Testes e verificações

- **Unitários (domínio):** tradução do mover (etiqueta exclusiva ou várias), casamento de regras, conversão regra ⇄
  filtro do Gmail nos dois sentidos, a heurística "um sumiu + um novo = edição".
- **Integração:** o adaptador IMAP/SMTP contra o GreenMail; endpoints contra um `IMailboxProvider` falso.
- **Manual:** o adaptador do Gmail contra uma conta real.
- **Titan, ainda a verificar:** se o envio SMTP já salva em Enviados (para evitar duplicatas), no início da M3. O
  CONDSTORE é oferecido e o ManageSieve não: veja a
  [verificação real](implementation-status.md#verificação-real-2026-10-30).

---

## 15. Fases

### M1 — Conectar e ler ✅

Pronta em 2026-10-30: veja o [Status de implementação](implementation-status.md).

### M2 — Organizar ✅

Pronta em 2026-10-31: veja o [Status de implementação](implementation-status.md).

### M3 — Enviar

- Escrever, responder, encaminhar, anexos, cópia em Enviados no IMAP.
- **Pronto quando:** uma resposta enviada pelo Pandora cai na mesma conversa do Gmail, e uma mensagem
  enviada pelo Titan aparece exatamente uma vez na pasta Enviados.

### M4 — Regras

- Modelo de regra, motor do Pandora (IMAP), filtros do Gmail nos dois sentidos, status `broken`,
  `mail/rules`.
- **Pronto quando:** os filtros existentes do Gmail aparecem como regras ao conectar; uma regra criada
  no Pandora aparece como filtro no Gmail e é aplicada na chegada com o Pandora parado; um filtro
  editado no Gmail atualiza a regra no lugar; uma regra do Titan move uma mensagem nova em um ciclo.

### Depois (sem data)

- ManageSieve no Titan (se houver), condições com IA nas regras, Radar consumindo
  `MailMessageReceived`, assinatura por caixa, rascunhos no servidor, busca no corpo pelo provedor,
  "aplicar às mensagens existentes", cache de anexos, push do Gmail (Pub/Sub) no lugar do polling,
  mais provedores (Outlook via Graph).

---

## 16. Questões em aberto

1. **Identidade de mensagem IMAP entre pastas.** Um `MOVE` dá um UID novo à mensagem no destino. A
   M1 identifica mensagens IMAP por (pasta, UID) e trata um mover feito no Pandora como atualização
   daquela linha (o adaptador devolve o UID novo via `COPYUID`). Um mover feito no webmail aparece como
   "removida aqui + nova lá"; se vale costurar as duas pelo `rfc822_message_id` é decidido na M2. (Decidido: são juntadas, veja o [plano da M2](execution-plan-m2.md).) Até
   lá, a M1 não pode disparar `MailMessageReceived` para uma mensagem cujo `rfc822_message_id` a caixa
   já tem, para um mover no webmail não ser confundido com chegada nova.
