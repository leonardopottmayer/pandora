# Módulo Projects — Plano de Produto

> **Status:** design registrado (2026-11-07); nada construído ainda. A entrega segue as etapas P1–P7 (§11).
> 🇺🇸 [English version](../en/product-plan.md)
>
> Relacionados: [Databases](../../databases/pt-BR/README.md) (tipos de campo, árvore de consulta) ·
> [Notes](../../notes/README.md) (o editor) · [Channels](../../channels/README.md) ·
> [Admin](../../admin/pt-BR/README.md) (flags e permissões)

---

## 1. O que o módulo faz

O **Projects** é um gerenciador de trabalho no espírito do Azure Boards e do Jira. O usuário cria
**projetos** e, em cada um, planeja e acompanha **itens de trabalho** (epics, features, stories, tasks,
bugs e qualquer tipo customizado) por **backlogs**, **sprints** e **boards** cuja esteira ele monta
livremente: estados por tipo e colunas de board que se aninham em grupos e subcolunas.

```
Projeto "Pandora" (chave PAN)
 ├── Membros e papéis             (por enquanto só o criador, §7)
 ├── Processo                     tipos de item, campos, workflows, níveis do backlog — copiados de um template, editáveis
 ├── Áreas, iterações (sprints), releases
 ├── Itens de trabalho            PAN-1 Epic › PAN-4 Feature › PAN-9 Story › PAN-12 Task
 ├── Boards                       kanban por nível do backlog, o quadro da sprint — árvores de colunas
 └── Consultas salvas, dashboards
```

### O que ele não é

- **Não são as tarefas do Agenda.** As tarefas do Agenda são to-dos pessoais com lembretes. Os itens do
  Projects pertencem ao processo de um projeto, com estados, hierarquia, estimativas e boards. Uma etapa
  futura pode mostrar as datas de entrega no calendário do Agenda (§9).
- **Não é compartilhado, ainda.** Por enquanto só o criador do projeto usa, mas o modelo nasce pronto para
  membros (§7), então adicioná-los é só acréscimo.
- **Não é controle de versão nem CI.** Commits e pull requests podem se ligar a itens depois (§9); o
  Projects nunca hospeda código.

---

## 2. Nomes e coordenadas

| Item | Valor |
|---|---|
| Nome do módulo | `Projects` (UI: "Projetos" em pt-BR, "Projects" em en) |
| Projetos do backend | `Pottmayer.Pandora.Modules.Projects.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| Schema PostgreSQL | `projects` |
| Prefixo de tabela | `prjXXX_`, PK `uuid_generate_v7()` |
| Seção de options | `Pandora:Projects` |
| Base da API | `/api/v{version}/projects`, JWT |
| Frontend | `client-web/src/modules/projects` (rotas no §8.6) |
| Migrations | `migrations/migrations/projects/` |
| Chave do item | `<CHAVE DO PROJETO>-<número>` (`PAN-123`), número por projeto, nunca reutilizado |

---

## 3. Princípios

1. **O processo é dado.** Tipos de item, campos, estados, transições, níveis do backlog e colunas de board
   são linhas que o usuário edita, não código. Os templates só fazem a carga inicial.
2. **Estados têm categorias.** Todo estado pertence a uma categoria fixa (`proposed`, `in-progress`,
   `resolved`, `completed`, `removed`). Relatórios, burndown, "itens abertos" e automações leem as
   categorias, então funcionam com qualquer workflow que o usuário invente.
3. **Colunas não são estados.** As colunas do board formam uma árvore; as folhas são ligadas a estados,
   várias folhas podem compartilhar um estado, e o lugar de cada item num board é guardado por board (§6).
4. **O acesso passa pela associação.** Nenhum código checa "é o dono do projeto". Toda checagem pergunta
   ao `IProjectAccess` o papel do usuário no projeto (§7).
5. **Pessoas são ids explícitos.** Responsável, relator, autor do comentário, autor de cada entrada do
   histórico: sempre um id de usuário, nunca "o usuário atual" implícito.
6. **Tudo deixa rastro.** Toda mudança num item é uma entrada de histórico com campo, valor antigo, valor
   novo, quem e quando.
7. **Peças compartilhadas.** Os campos customizados usam o mesmo motor de tipos de campo do
   [Databases](../../databases/pt-BR/README.md) (`IFieldType`), e as consultas a mesma árvore de consulta.
   O módulo construído primeiro extrai o motor para um projeto compartilhado; o segundo reaproveita.

---

## 4. Processo

### 4.1 Templates

Um projeto é criado a partir de um template e ganha sua própria **cópia**: mudar um projeto nunca muda
outro.

| Template | Níveis do backlog e tipos | Estados por tipo |
|---|---|---|
| **Básico** | Epics: Epic · Issues: Issue · Tasks: Task | To Do → Doing → Done |
| **Ágil** | Epics: Epic · Features: Feature · Stories: User Story, Bug · Tasks: Task | New → Active → Resolved → Closed (+ Removed) |
| **Scrum** | Epics: Epic · Features: Feature · Backlog items: Product Backlog Item, Bug · Tasks: Task, Impediment | New → Approved → Committed → Done (+ Removed) |

Os templates vêm no código como definições de carga; eles também criam os boards padrão e suas colunas (§6).

### 4.2 Tipos de item

Cada tipo tem: nome, ícone, cor, o nível do backlog a que pertence, os tipos permitidos como pai, os
campos e o layout do formulário, o workflow, um template de descrição opcional (por exemplo, um bug com
"Passos / Esperado / Obtido") e uma marcação de habilitado. Desabilitar um tipo o esconde na criação sem
mexer nos itens existentes.

### 4.3 Níveis do backlog

Os níveis ordenam a hierarquia: níveis de **portfólio** (Epics, Features — quantos o usuário quiser), um
nível de **requisito** (Stories, PBIs, Bugs) e um nível de **task**. O quadro da sprint trabalha no nível
de task abaixo do nível de requisito; cada um dos outros níveis ganha um backlog e um board kanban.

### 4.4 Workflows

Por tipo:

- **Estados:** nome, cor, categoria (§3), ordem. Cada categoria de `proposed` a `completed` precisa de pelo
  menos um estado; `removed` é opcional.
- **Transições:** opcionais. Sem nenhuma, qualquer estado vai para qualquer estado; com algumas, só elas
  valem.
- **Motivos:** uma lista opcional por transição ("Closed → Duplicado", "Active → Reaberto").

Remover um estado pede um estado de destino; os itens nele vão para lá, com uma entrada no histórico.

### 4.5 Campos

**Campos de sistema** são colunas do item (§8.1): título, descrição, critérios de aceite, estado, motivo,
prioridade, severidade, responsável, relator, área, iteração, release, pai, tags, data de início, data de
entrega, data alvo, story points, esforço, trabalho original/restante/concluído, passos para reproduzir,
ambiente, checklist. Um tipo pode esconder os que não usa.

**Campos customizados** são definidos uma vez por projeto e adicionados aos tipos que os usam, cada um com
obrigatoriedade e valor padrão. Os valores vivem no `custom jsonb` do item, chaveados pelo id do campo. Os
tipos vêm do motor de campos compartilhado: texto, texto longo, número, decimal, data, data e hora,
booleano, escolha única e múltipla, URL, usuário (qualquer membro do projeto).

Listas que o usuário edita por projeto: prioridades, severidades, tags.

### 4.6 Rich text

Descrição, critérios de aceite, passos para reproduzir e comentários usam o editor do
[Notes](../../notes/README.md) e o formato de armazenamento dele. Os critérios de aceite também podem ser
um checklist.

---

## 5. Planejamento

### 5.1 Backlogs

- Um backlog por nível, como lista ou como árvore (o nível com seus filhos).
- Ordenado por um **rank fracionário** (uma string entre os vizinhos, no estilo LexoRank): arrastar um item
  atualiza uma linha. O mesmo rank ordena os cards dentro de uma coluna do board.
- Criação rápida inline, seleção múltipla com ações em lote (§8.4), filtros e "mostrar concluídos"
  desligado por padrão.

### 5.2 Iterações e sprints

- As iterações formam uma árvore (`2026 › T4 › Sprint 23`); as iterações-folha com início e fim são
  sprints.
- Uma sprint tem meta e estado (`planned`, `active`, `closed`).
- **Capacidade:** horas por dia e folgas por membro, mais as folgas do projeto. O backlog da sprint mostra o
  trabalho planejado (trabalho restante, ou pontos) contra a capacidade.
- **Fechar uma sprint:** os itens fora da categoria `completed` vão para a próxima sprint ou voltam ao
  backlog, à escolha do usuário; a mudança entra no histórico de cada item.

### 5.3 Áreas e releases

As áreas formam uma árvore (`Backend › Identity`). Releases têm nome, data alvo e estado; os itens se
ligam a uma release.

---

## 6. Boards

### 6.1 A árvore de colunas

As colunas de um board são uma árvore de qualquer profundidade (a tela suporta três níveis por enquanto):

```
| Backlog | Dev                  | Rollout                                          | Done |
|         | Fazendo  | Code rev. | Liberação em produção | Acompanhamento pós lib.  |      |
```

- **Nós de grupo** são só cabeçalho. Eles contêm colunas filhas e podem ter um limite de WIP que conta
  todos os cards abaixo deles.
- **Colunas-folha** recebem cards. Cada uma tem nome, cor, limite de WIP opcional e uma "definição de
  pronto" opcional mostrada no hover.
- Cada folha é ligada a **um estado por tipo de item** mostrado no board ("Liberação em produção" = User
  Story em *Resolved*, Bug em *Resolved*). Várias folhas podem ser ligadas ao mesmo estado.

### 6.2 Onde o card fica

A folha do item num board é guardada por board (`prj020`):

- **Mover um card** para uma folha muda o estado do item para o estado ligado àquela folha para o tipo
  dele; se o estado é o mesmo (duas folhas compartilhando), só muda a posição no board. As duas coisas
  entram no histórico.
- **Estado mudado em outro lugar** (o formulário, uma edição em lote, o Assistant, uma automação): em todos
  os boards, o card vai para a primeira folha, na ordem das colunas, ligada ao novo estado.
- **Validação ao salvar:** todo estado de todo tipo do board precisa estar ligado a pelo menos uma folha,
  para nenhum card se perder.
- Transições que o workflow proíbe fazem a coluna de destino recusar o card.

### 6.3 Raias, cards e estilos

- **Raias:** uma lista por board com uma raia padrão; a raia do card é guardada junto com a posição. As
  raias também podem vir de um campo (prioridade, responsável) em vez de serem manuais.
- **Configuração do card:** quais campos aparecem (id, responsável, tags, pontos, trabalho restante,
  progresso dos filhos, data de entrega) por tipo.
- **Regras de estilo:** condição (a árvore de consulta, §8.3) → cor do card ou da tag. "Severidade =
  Crítica → vermelho".

### 6.4 Tipos de board

| Tipo | Mostra | Criado |
|---|---|---|
| `kanban` | Os itens de um nível do backlog, opcionalmente restritos por um filtro | Um por nível junto com o projeto, mais quantos boards extras o usuário quiser |
| `sprint` | Os filhos de nível task dos itens da sprint selecionada, uma linha por pai | Um por projeto; o mesmo motor de colunas, com a árvore própria |

Os dois tipos compartilham o motor do §6.1–§6.3, então o quadro da sprint é tão customizável quanto o
kanban.

---

## 7. Acesso: um usuário agora, membros depois

- **`prj002_member`** existe desde a primeira etapa. Criar um projeto adiciona o criador como `admin`; por
  enquanto é a única linha.
- **Papéis:** `admin` (processo, boards, membros, tudo), `member` (itens, comentários, sprints), `reader`
  (só leitura). No começo só o `admin` é usado.
- **Um único ponto de checagem:** `IProjectAccess.RequireAsync(projectId, role)` em todo handler. As
  listas filtram por "projetos dos quais sou membro", nunca por uma coluna de criador.
- **Em cima do Admin:** as permissões globais (§8.5) dizem o que o usuário pode fazer no módulo; o papel no
  projeto diz o que ele pode fazer naquele projeto.
- **Concorrência desde o início:** os itens têm versão; uma atualização desatualizada recebe 409 e o
  cliente recarrega. Barato com um usuário, necessário com vários.
- **Depois, só acréscimo:** tela de membros e convites, os papéis `member` e `reader` na UI, @menções,
  observadores, notificações para outras pessoas.

---

## 8. Pandora: o módulo Projects

### 8.1 Modelo de dados

| Tabela | O que guarda | Colunas principais |
|---|---|---|
| `prj001_project` | Um projeto | `id`, `key` (única), `name`, `description`, `template`, `item_sequence`, `created_by`, `archived_at`, timestamps |
| `prj002_member` | Associação | `project_id`, `user_id`, `role`, `added_at`; PK nas duas primeiras |
| `prj003_backlog_level` | Um nível do backlog | `id`, `project_id`, `name`, `kind` (`portfolio`/`requirement`/`task`), `position` |
| `prj004_item_type` | Um tipo de item | `id`, `project_id`, `backlog_level_id`, `name`, `icon`, `color`, `allowed_parent_type_ids uuid[]`, `form_layout jsonb`, `description_template`, `hidden_system_fields text[]`, `is_enabled`, `position` |
| `prj005_state` | Um estado do workflow | `id`, `item_type_id`, `name`, `color`, `category`, `position` |
| `prj006_transition` | Uma transição permitida | `from_state_id`, `to_state_id`, `reasons text[]` |
| `prj007_field` | Um campo customizado | `id`, `project_id`, `name`, `type`, `config jsonb` |
| `prj008_item_type_field` | Um campo num tipo | `item_type_id`, `field_id`, `is_required`, `default_value jsonb` |
| `prj009_area` | Uma área | `id`, `project_id`, `parent_id`, `name`, `position` |
| `prj010_iteration` | Uma iteração ou sprint | `id`, `project_id`, `parent_id`, `name`, `start_date`, `end_date`, `goal`, `state`, `position` |
| `prj011_release` | Uma release | `id`, `project_id`, `name`, `target_date`, `state`, `description` |
| `prj012_item` | Um item de trabalho | `id`, `project_id`, `number`, `item_type_id`, `parent_id`, `title`, `description`, `acceptance_criteria`, `steps_to_reproduce`, `environment`, `state_id`, `reason`, `priority`, `severity`, `assignee_id`, `reporter_id`, `area_id`, `iteration_id`, `release_id`, `rank`, `story_points`, `effort`, `original_work`, `remaining_work`, `completed_work`, `start_date`, `due_date`, `target_date`, `tags text[]`, `checklist jsonb`, `custom jsonb`, `version`, `created_by`, `state_changed_at`, `closed_at`, timestamps |
| `prj013_item_link` | Um vínculo entre itens | `from_item_id`, `to_item_id`, `kind` (`related`, `blocks`, `duplicates`, `predecessor`), `created_by`, `created_at` |
| `prj014_comment` | Um comentário | `id`, `item_id`, `author_id`, `body`, `created_at`, `edited_at`, `deleted_at` |
| `prj015_item_history` | Uma revisão | `id`, `item_id`, `changed_by`, `changed_at`, `changes jsonb` (`[{ field, old, new }]`) |
| `prj016_board` | Um board | `id`, `project_id`, `kind`, `name`, `backlog_level_id`, `filter jsonb`, `card_settings jsonb`, `style_rules jsonb`, `lane_field`, `position` |
| `prj017_board_column` | Um nó de coluna | `id`, `board_id`, `parent_id`, `name`, `color`, `wip_limit`, `definition_of_done`, `position` |
| `prj018_board_column_state` | O estado de uma folha por tipo | `column_id`, `item_type_id`, `state_id`; PK nas duas primeiras |
| `prj019_board_lane` | Uma raia manual | `id`, `board_id`, `name`, `is_default`, `position` |
| `prj020_board_item` | O lugar de um card | `board_id`, `item_id`, `column_id`, `lane_id`; PK nas duas primeiras |
| `prj021_capacity` | Capacidade da sprint | `iteration_id`, `user_id`, `hours_per_day`, `days_off date[]` |
| `prj022_saved_query` | Uma consulta salva | `id`, `project_id`, `owner_id`, `name`, `query jsonb`, `is_shared` |

Único em `prj012 (project_id, number)`. O próximo número sai de `prj001.item_sequence` num
`UPDATE ... RETURNING`, então duas criações nunca pegam o mesmo. Itens removidos ficam (categoria
`removed`); a exclusão definitiva é uma ação de admin.

Tabelas para depois: anexos, reações a comentários, observadores, dashboards e widgets, apontamento de
horas, regras de automação, templates de item.

### 8.2 Campos customizados

O `custom jsonb` do item é chaveado pelo id do campo, como o `data` de um registro do Databases. Renomear
um campo mexe em uma linha; excluir um tira a chave dele dos itens do projeto. Validação, operadores de
filtro, expressões SQL, import e export vêm do `IFieldType` de cada campo.

### 8.3 Consultas

- Filtros, ordenações e colunas são uma árvore de consulta (o formato do Databases), sobre campos de
  sistema, campos customizados e alguns calculados (`@me`, `@current-sprint`, `@today`, "tem filhos",
  "o pai é").
- **Consultas salvas:** pessoais ou compartilhadas no projeto; resultado em lista simples ou em árvore.
- **Sintaxe de texto** (depois): `type = Bug and state in category in-progress and assignee = @me`.

### 8.4 Edição em lote

De um backlog, um board ou uma consulta, vários itens de uma vez: estado, responsável, iteração, área,
release, prioridade, tags, pai, um campo customizado. Cada item ganha a própria entrada no histórico.

### 8.5 Flags e permissões

Declaradas em `Projects.Abstractions` como constantes (`ProjectsFeatures`, `ProjectsPermissions`):

| Tipo | Chave | Protege |
|---|---|---|
| Feature | `projects` | O módulo inteiro |
| Permissão | `projects.projects.{read,create,update,delete}` | Projetos e suas configurações (processo, boards, áreas, iterações) |
| Permissão | `projects.items.{read,create,update,delete}` | Itens, vínculos, comentários |

O papel padrão `user` recebe todas. Dentro de um projeto, quem decide é o papel do §7.

### 8.6 API e frontend (esboço)

| Área | API (sob `/projects`) | Tela |
|---|---|---|
| Projetos | `GET/POST /`, `GET/PUT/DELETE /{key}` | `projects` — lista; `projects/:key` — resumo |
| Processo | `/{key}/process/types`, `/states`, `/transitions`, `/fields`, `/levels` | `projects/:key/settings/process` — tipos, campos, editor de workflow |
| Itens | `POST /{key}/items`, `GET/PATCH/DELETE /items/{itemKey}`, `/items/{itemKey}/comments`, `/links`, `/history` | `projects/:key/items/:itemKey` — formulário completo; um drawer de qualquer lugar |
| Backlogs | `GET /{key}/backlogs/{levelId}`, `POST /items/{itemKey}/rank` | `projects/:key/backlog/:level` |
| Sprints | `/{key}/iterations`, `/iterations/{id}/capacity`, `POST /iterations/{id}/close` | `projects/:key/sprints/:iterationId` — backlog, board, capacidade |
| Boards | `/{key}/boards`, `/boards/{id}/columns`, `/lanes`, `POST /boards/{id}/move` | `projects/:key/boards/:boardId`, configurações do board |
| Consultas | `POST /{key}/query`, `/{key}/queries` | `projects/:key/queries` |
| Lote | `POST /{key}/items/bulk` | A partir das listas e dos boards |

---

## 9. Pontos de extensão e features futuras

| Ponto | O que permite |
|---|---|
| Motor de campos (`IFieldType`) | Novos tipos de campo customizado, compartilhados com o Databases |
| Árvore de consulta | Sintaxe de texto, widgets de dashboard, regras de estilo, condições de automação, filtros de board |
| Categorias de estado | Relatórios e automações que funcionam com qualquer workflow |
| Tipos de board | Mais layouts sobre o mesmo motor de colunas |
| Eventos de integração | `WorkItemCreated/Changed` pelo outbox, quando houver um consumidor |

Planejados para depois, cada um com seu plano:

- **Relatórios:** burndown/burnup da sprint, velocidade, fluxo cumulativo, lead time e cycle time, bugs
  abertos × fechados. **Dashboards** por projeto com widgets. **Roadmap:** linha do tempo de epics e
  features com dependências e releases.
- **Colaboração:** membros e convites, @menções, reações, observadores, notificações pelo Channels
  (atribuído, mencionado, item observado mudou, sprint começa/termina), anexos.
- **Produtividade:** templates de item, atalhos de teclado, apontamento de horas, regras de automação
  ("quando todos os filhos estiverem concluídos, resolver o pai").
- **Integrações:** o Assistant ("cria um bug no PAN: login falha com MFA"), datas de entrega e sprints no
  calendário do Agenda, wiki do projeto e vínculo item ↔ página no Notes, GitHub (um `PAN-123` num commit
  ou PR cria o vínculo; um PR mergeado pode mover o estado), import e export CSV/Excel, import de CSV do
  Jira e do Azure.

---

## 10. Decisões

| # | Decisão | Escolhida | Em vez de |
|---|---|---|---|
| 1 | Usuários | Um usuário agora, o modelo de associação desde o início | Membros agora; modelo só de dono refatorado depois |
| 2 | Processo | Template copiado para cada projeto, editável nele | Processos globais herdados; tipos fixos |
| 3 | Campos customizados | Campos de sistema como colunas, customizados em `jsonb` pelo id do campo, motor de campos compartilhado com o Databases | Tudo em `jsonb` |
| 4 | Ordem | Rank fracionário em string | Posições inteiras |
| 5 | Rich text | O editor do Notes | Markdown em textarea |
| 6 | Colunas e estados | Árvore de colunas; folhas ligadas a estados; lugar guardado por board | Coluna = estado; colunas independentes dos estados |
| 7 | Nome | `Projects` | `Work`; `Boards` |

---

## 11. Entrega

| Etapa | Entrega | Pronto quando |
|---|---|---|
| **P1** | Projetos, chave e numeração, associação com o criador, templates, itens com campos de sistema, hierarquia, comentários, histórico | Um projeto do template Ágil tem uma árvore epic › feature › story › task com comentários e o histórico de cada mudança |
| **P2** | Workflows (estados, categorias, transições, motivos), o editor de processo (tipos, campos customizados, níveis do backlog, layout do formulário) | Um tipo customizado "Spike" com estados próprios e dois campos customizados é criado e usado |
| **P3** | O motor de boards (árvore de colunas, ligação com estados, WIP, raias, cards, estilos), kanban por nível e boards extras | O board "Rollout › Liberação em produção / Acompanhamento pós liberação" funciona, as duas compartilhando *Resolved* |
| **P4** | Backlogs com rank, iterações e sprints, capacidade, o quadro da sprint com colunas próprias, fechamento da sprint | Uma sprint é planejada contra a capacidade, tocada no board dela e fechada, movendo o que sobrou |
| **P5** | Consultas e consultas salvas, edição em lote, vínculos | "Meus bugs abertos na sprint atual" fica salva e é movida em lote para a próxima sprint |
| **P6** | Relatórios, dashboards, roadmap | O burndown da sprint e a velocidade das últimas sprints aparecem num dashboard |
| **P7+** | Membros e o resto do §9 | Uma de cada vez, cada uma com seu plano |
