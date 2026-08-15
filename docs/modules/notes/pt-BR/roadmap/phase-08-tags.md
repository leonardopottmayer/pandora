# Fase 08 — Tags (`#tag` no markdown) + filtros

> Bloco no plano: MVP §3 ("Tags, favoritos, arquivar — ✅ mínimo") · Depende de: 04, 05, 06 ·
> Camada: backend + frontend

## Objetivo

Fechar a única capacidade que o plano marcou como MVP e nunca foi implementada. A Fase 01
adiou tags ("opcional no MVP; pode espelhar o padrão do Finances") e nada as trouxe depois —
o que deixou o **filtro por tag do grafo** (registrado como não feito na Fase 06) sem o que
filtrar, e os "filtros" da busca (§3, v2) sem base.

## Decisões desta fase (o que o plano deixou em aberto)

### Modelo: híbrido — conteúdo manda, tabela guarda a cor

A tag é **escrita no markdown** (`#ideias`), como no Obsidian, e o backend materializa as
arestas no save — a mesma mecânica já provada do `PageLink` na Fase 04. Isso mantém a
promessa central do módulo: *markdown é a fonte da verdade*, e um `.md` exportado leva as
tags junto.

Sobre isso mora uma tabela `Tag` que guarda o que o texto não sabe dizer: **cor** e o nome
como foi escrito da primeira vez. Ela não é gerenciada por CRUD — nasce quando uma tag
aparece num conteúdo e é regida pelo texto.

> Consequência aceita: **não há rename global de tag**. Renomear exigiria reescrever o
> markdown de todas as pages — é `find & replace`, não uma operação de banco. Fica registrado.

### Uma tag "vazia" só sobrevive se tiver cor

Quando a última page para de citar uma tag, a linha `Tag` é **apagada** — a menos que tenha
cor definida. A cor é a única coisa que o usuário investiu ali e que o texto não recupera;
sem ela, uma tag sem pages é só lixo aparecendo nos filtros.

### Filtro com várias tags é **E** (interseção)

Nas quatro superfícies. Duas tags selecionadas mostram as pages que têm **as duas**. É a
semântica de "ir estreitando" — e uma regra só, igual em todo lugar, é mais previsível do
que OU no grafo e E na busca.

### A sidebar filtrada vira lista, não árvore

Filtrar uma árvore por tag quebra a hierarquia: uma filha casa e a mãe não. Em vez de
inventar regra de ancestral, com filtro ativo a sidebar mostra as pages que casaram **em
lista plana** — é o que a pergunta "quem tem essa tag?" quer ver.

## Escopo

### Backend — domínio

- `TagParser`: extrai `#tag` do markdown. Só vale `#` no **início da linha ou depois de
  espaço** (então `http://x#frag` e `src/lib#2` não disparam); caracteres aceitos são letras,
  dígitos, `-`, `_` e `/` (tag aninhada, estilo Obsidian); precisa ter **ao menos uma letra**
  (`#123` é número, não tag); heading (`# Título`) não casa porque exige espaço depois do `#`.
  **Blocos de código (cercados e inline) são removidos antes do parse** — um `#comentário`
  dentro de um bloco bash não é uma tag.
- `TagName`: normaliza para a chave única (`slug`) — minúsculas, acentos removidos, mantendo
  `/`, `-` e `_`. `#Café` e `#cafe` são a mesma tag; o **nome exibido** é como foi escrito.
- `Tag` (nte005): `Id, UserId, Slug, Name, Color?` + auditoria.
- `PageTag` (nte006): `Id, PageId, TagId, CreatedAt` — aresta derivada, igual ao `PageLink`.

### Backend — aplicação

- `PageTagSynchronizer` (espelha o `PageLinkSynchronizer`): parse do conteúdo → resolve/cria
  as `Tag` do usuário → **diff** das arestas (idempotente, sem apagar-e-recriar) → limpa as
  tags que ficaram órfãs e sem cor. Chamado no create e no update; o delete remove as arestas
  da page.
- `GetTagsQuery` → `TagDto(Id, Slug, Name, Color, PageCount)`.
- `UpdateTagCommand` → só **cor** (rename global está fora, ver acima).
- Filtro `TagIds` em `GetPageTreeInput`, `SearchPagesInput` e `GetPageGraphInput`. No grafo,
  o corte é **antes** da vizinhança, como a nota da Fase 06 previu.
- Busca com tags e `q` vazio **lista as pages daquela tag** (é como se navega uma tag).
- `PageDto` passa a carregar as tags da page.

### Backend — persistência e migrations

- `notes.nte005_tag` (único por `user_id + slug`) e `notes.nte006_page_tag` (único por
  `page_id + tag_id`), em `migrations/migrations/notes`.

### Presentation

- `GET /api/v1/notes/tags`, `PUT /api/v1/notes/tags/{id}` (cor).
- `tagIds` como query param em `GET /notes/pages`, `/notes/pages/search`,
  `/notes/pages/graph` e `/notes/pages/{id}/graph`.

### Frontend

- `lib/tags.ts` espelha o parser do backend (o preview precisa resolver igual ao save vai
  resolver — mesmo motivo do `wikilinks.ts`), mais o gatilho de autocomplete de `#`.
- `#tag` renderizada como chip colorido no preview; autocomplete de `#` no editor, ao lado
  dos menus de `[[` e `/`.
- Chips das tags na page aberta; clicar numa filtra a sidebar por ela.
- Filtro (multi-seleção) na sidebar, no palette de busca e na `NotesGraphPage`.
- Escolher a cor de uma tag pela UI do filtro.
- i18n EN + PT-BR.

## Verify

- Escrever `#ideias` numa page e salvar cria a tag e a aresta; apagar do texto remove a
  aresta (idempotente, sem duplicata).
- `#Café` e `#cafe` casam na mesma tag; `#123`, `# Título` e `#comentário` dentro de bloco
  de código **não** viram tag.
- Filtrar por duas tags devolve só as pages que têm as duas — na sidebar, na busca e no grafo.
- Cor definida sobrevive à tag ficar sem pages; tag sem cor e sem pages some.
- Clicar no chip de uma tag na page filtra a sidebar por ela.

## Fora de escopo

- **Rename global de tag** (exigiria reescrever o markdown de todas as pages).
- Hierarquia real de tags aninhadas (`#projeto/pandora` é aceito como texto da tag; não há
  rollup de `#projeto` incluindo as filhas).
- Ranking/realce da busca — segue pendente do §3, é outra fase.

## Notas de implementação (fase concluída)

- Tabelas `notes.nte005_tag` (única por `user_id + slug`) e `notes.nte006_page_tag` (única por
  `page_id + tag_id`). O save faz **diff** das arestas, como o `PageLinkSynchronizer` — re-salvar o
  mesmo texto não toca em nada.
- **O EF precisou conhecer a FK** `page_tag → tag`, mesmo sem navegação
  (`HasOne<Tag>().WithMany().HasForeignKey(...)`): a varredura apaga a tag órfã e as arestas dela na
  mesma transação, e sem a relação declarada o EF mandava o `DELETE` da tag primeiro, batendo em
  `fk_nte006_tag`. Foi um teste de integração que pegou isso.
- `TagParser` **remove os blocos de código antes de procurar** (cercados e inline), substituindo-os
  por espaços do mesmo tamanho para não mexer nos inícios de linha — `#` é comum demais em prosa e
  em shell para se confiar só no formato.
- `TagName.ToSlug` mantém `/`, `-` e `_` (ao contrário do `Slugger`, que achata tudo em hífen):
  `#projeto/pandora` é uma tag só. Exige ao menos uma letra, então `#123` não é tag.
- A varredura de órfãs desconta as arestas removidas na mesma transação (o repositório ainda as
  enxerga) passando o id da page que está sendo salva.
- **Cor**: só hex (`#rgb`/`#rrggbb`/`#rrggbbaa`), validado no handler — ela vai inline no `style` do
  chip. Resposta a cor inválida é **422**, que é como o projeto mapeia erro de validação.
- Frontend: `lib/tags.ts` espelha o parser. O regex teve que ser montado com strings **aspeadas** (a
  crase escapada é escape inválido sob a flag `u`) e a cerca é escrita duas vezes — fechada e
  correndo até o fim do texto —, porque `$` ali significaria fim de *linha*, já que a flag `m` é
  necessária para a regra de início de linha da tag.
- O chip do preview usa `color`/`border-color` inline, não uma custom property: o HTML passa pelo
  DOMPurify, e o filtro de CSS dele é motivo para não depender de nada exótico sobreviver.
- Sidebar filtrada **desliga o drag-and-drop**: a lista deixou de ser a árvore, e um drop estaria
  reordenando contra irmãs que não estão na tela.
- A busca lê **mais** que o teto quando há filtro de tag (`ResultLimit * 10`) e corta depois: cortar
  antes deixaria de fora uma page que casa com os dois critérios.
- Autocomplete de `#` só completa tags **existentes** — tag nova se digita, que é o ponto de ela
  morar no texto. Fica ao lado dos menus de `[[` e `/`, com o mesmo `filter: false` e a mesma
  entrada por ref.

### Registrado (não feito)

- **Sem preview do filtro na rota**: os filtros são estado de componente, não entram na URL — não dá
  para mandar link de "as pages com #x". O lugar seria um search param na `NotesPage`.
- ~~Sem seletor de emoji para o ícone da page~~ — **feito depois**, ainda nesta sessão, no lugar
  previsto: um botão à esquerda do título na `NotesPage`, abrindo um popover com campo livre, uma
  fileira de sugestões e "remover".
  - Sem lib de emoji: todo sistema já traz um painel com busca (Win+. no Windows), e o que se digita
    é reduzido ao **primeiro grafema** (`Intl.Segmenter`), então o campo não vira um segundo título.
    Grafema e não caractere porque emoji costuma ser vários code points — bandeira, tom de pele,
    família —, e cortar por índice devolveria outro emoji, ou metade de um.
  - O ícone entrou no `PageDraft`, então ele salva pelo **mesmo autosave** do título e do corpo. Antes
    o `handleSave` reenviava `page?.icon` só para não apagar o que já estava lá; agora manda o do
    rascunho.
