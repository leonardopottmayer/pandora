# Status de implementação

[← Voltar ao índice](README.md)

## Reforma em blocos — atualização de 2026-11-07

B1–B10 estão construídas, sem commit. A primeira fatia da B9 acrescenta resumir/melhorar/traduzir
seleção e continuar pelo menu `/`, com prévia antes de aplicar, configuração do Assistant e origem
`assistant` no autosave aceito. Ver [plano da reforma](product-plan.md). B11 inclui captura de e-mail, resumos de Finanças e Agenda Hoje/tarefas/próximos eventos. Validação no navegador e com provedor real pendente. As seções históricas abaixo
descrevem a implementação anterior em markdown até a atualização de sua documentação.

A segunda fatia da B9 acrescenta o resumo da página inteira no menu de opções da página editável:
salvar edições pendentes, gerar prévia e aceitar como novo destaque Resumo no topo. A rota valida o
dono e lê texto salvo (incluindo tabelas/propriedades/chips), com limite explícito de 32000 caracteres
e sem gravar página até aceitar.

A terceira fatia da B9 acrescenta “Perguntar às notas” ao lado da busca, com filtro opcional por espaço.
Busca até cinco páginas salvas do dono por alternativas de palavras-chave, envia trechos limitados ao
Assistant configurado e aceita citações apenas dessas páginas. A resposta em texto simples mostra
fontes clicáveis e trechos; sem resultado/evidência suficiente aparece uma mensagem explícita. Não
grava notas nem conversas. A busca lexical não encontra sinônimos/relações semânticas. Bloco de IA e
captura no Telegram estão descritos abaixo. Navegador e provider real continuam pendentes.

A quarta fatia da B9 acrescenta `/Bloco de IA`: prompt salvo, resultado aceito como texto simples e
instante UTC de geração do servidor. Gerar/regenerar usa o perfil existente do Assistant, mostra prévia,
mantém o resultado anterior ao descartar/falhar e marca a aceitação como origem Assistente. O resultado
entra na busca, no texto legível, nas exportações e no diff de versões. Leitura/link público só exibem
o conteúdo salvo; o prompt também faz parte da página compartilhada.

A última fatia da B9 acrescenta captura direta `/nota <texto>` no bot Assistant Telegram vinculado,
sem IA/perfil/chave. Cria/reutiliza página Captura dedicada, data no fuso da conta, texto literal e
histórico/busca/datas de bloco com origem Assistente. Recibos em `nte015_note_capture` impedem salvar
de novo mesmo após purgar a página. Caixa travada recusa; caixa excluída é substituída. Exige
permissões/flags normais de Notes/Assistant. B9 construída, sem commit; testes reais de Telegram,
navegador e provedor pendentes. B10 construída abaixo; próxima etapa: B11.

Uma fotografia do que está construído no código versus o que está previsto mas ainda não
implementado. Serve para separar "documentado porque existe" de "documentado como plano".

O módulo foi construído em nove fases sequenciais (scaffold → pages → anexos → editor → wikilinks →
busca → grafo → blocos ricos → tags), todas **fechadas**. O que segue é o estado que elas deixaram,
não o log das fases.

---

## Implementado

| Área | Notas |
|---|---|
| **Revisão do frontend** | Espaços, biblioteca, seletor de mídia, grafo e editor exibem falhas de consulta em vez de resultados vazios. Uma página indisponível sai do estado de carregamento e exibe uma mensagem explícita. |
| **Scaffold do módulo** | Todos os projetos por camada, schema `notes`, registrado no Host. Sem jobs, sem eventos de integração, sem event log de auditoria. |
| **Pages** | Aggregate `Page`, árvore com rejeição de ciclo no reparent, slugs congelados com sufixo em colisão, mover/reordenar, favoritar, arquivar, soft delete da subárvore inteira (`nte001`). |
| **Espaços** | Aggregate `Space`, um padrão por usuário (criado na hora quando não há), pages num espaço, mover entre espaços levando a subárvore, wikilinks preferindo o próprio espaço, filtro de espaço na árvore / busca / grafo, tela de espaços e seletor na sidebar, `create_note` aceitando espaço (`nte007`). |
| **Ícone emoji** | Picker ao lado do título, reduzido ao primeiro grafema via `Intl.Segmenter`, salvo pelo autosave da própria page. |
| **Biblioteca de mídia** | Itens (imagem / arquivo; vídeo no schema, recusado no upload) em pastas virtuais, miniaturas WebP com ImageSharp e rotação EXIF, mover pastas sem ciclo, pasta excluída subindo o conteúdo, soft delete mantendo os bytes servidos, cache imutável, a tela da biblioteca, colar/arrastar no editor para "Não organizadas" (`nte002`, `nte003`, `nte008`, `nte009`). |
| **Mídia dentro das notas** | Seletor da biblioteca por `/imagem`, `/arquivo` e pela barra, largura, alinhamento e legenda pelo texto alternativo (`![texto|400|direita]`), `<img>` da biblioteca em HTML, zoom em tela cheia, cartões de arquivo com tamanho, quais notas usam cada item derivado no save (`nte010`), "Usado em" na biblioteca, lugar "Sem uso", espaço ocupado (total e sem uso), aviso antes de excluir um item em uso. |
| **Editor** | Editor visual estilo Notion (TipTap 3, salvando markdown: menus `/` `[[` `#`, barra da seleção, barra da tabela, alça de arrastar, callouts e blocos recolhíveis nativos com corpo e título editáveis, player sob o link de vídeo, cartão de arquivo da biblioteca, HTML inline preservado; HTML não suportado preservado como bloco cru) como padrão; CodeMirror 6 com markdown cru, preview e modo split ao lado; autosave de 800 ms, sanitização com DOMPurify. |
| **Blocos ricos** | Slash commands (14 blocos + 6 callouts), callouts Obsidian como extensão do `marked` (recolhíveis com `-`/`+`), blocos recolhíveis (`<details>`), player de YouTube/Vimeo a partir do link sozinho e allowlist de iframe (YouTube, Vimeo, Google Maps) com sandbox fixo, tabelas markdown assistidas com Tab/Shift+Tab e reformatação idempotente. |
| **Autocomplete** | Três menus — `[[` (pages por título e slug), `#` (tags existentes), `/` (comandos) — compartilhando `filter: false`, entradas por ref e tooltip pendurada no body. |
| **Wikilinks e backlinks** | `WikilinkParser`, resolução título-depois-slug, tipos `wikilink`/`embed`, reconstrução por diff no create e no update, endpoint e painel de backlinks, create-on-click (`nte004`). |
| **Busca** | `tsvector` gerado + GIN, configuração `simple`, `tsquery` seguro por prefixos, prioridade de título e relevância por `ts_rank_cd` com desempate estável, filtros antes do limite de 20, trecho de 160 caracteres com destaque seguro no título/trecho, palette no Ctrl+K e botão na sidebar. |
| **Grafo** | Grafo global e local a partir de uma query, BFS não-direcionada em memória com profundidade clampada em 1..5, grau calculado no backend, arestas quebradas filtradas, canvas `react-force-graph-2d` com destaque no hover e rótulos por zoom. |
| **Tags** | `TagParser` removendo código, normalização por `TagName`, criação de tag a partir do texto, arestas por diff, varredura de órfãs preservando tags coloridas, validação hex da cor, filtros por interseção na sidebar / busca / grafo, chips no preview (`nte005`, `nte006`). |
| **Famílias no grafo** | Regiões com nome/cor por tags escolhidas, prioridade ordenada sem duplicar páginas, contagens e enquadramento por família. Flag global desligada por padrão; modo e configuração lembrados por conta/navegador. Ver [Visão em grafo](graph-view.md#6-famílias-por-tags-e-regiões-temáticas). |
| **Famílias recolhidas no grafo** | Controles por visita substituem os membros por um resumo e agregam links externos por direção/tipo. A busca revela páginas escondidas automaticamente e o grafo original é preservado. Ver [Recolher famílias](graph-view.md#10-recolher-e-expandir-famílias). |
| **Controles da câmera do grafo** | Botões acessíveis de aumentar/diminuir zoom e enquadrar tudo nos grafos global/local, inclusive no celular; enquadramento manual tem prioridade ao terminar a simulação. Ver [Controles da câmera](graph-view.md#11-controles-da-câmera). |
| **Visibilidade do grafo** | Mostrar arquivadas controla sidebar e grafo global (desligado por padrão); Ocultar isoladas remove vértices sem links restantes. Busca, detalhes, contagens e enquadramento usam a mesma projeção filtrada, com graus recalculados. Escolhas por visita; sem alteração de API. Ver [Filtros de visibilidade](graph-view.md#5-filtros-de-visibilidade). |
| **Inspeção no grafo** | Busca por título e seleção de vértices destacam a vizinhança e mostram um painel responsivo com tags, famílias e notas conectadas sem duplicação. A ação Abrir nota navega para o editor. Ver [Localizar uma nota](graph-view.md#7-localizar-uma-nota) e [Inspecionar uma nota](graph-view.md#9-inspecionar-uma-nota-sem-sair-do-grafo). |
| **Frontend** | Módulo React (`client-web/src/modules/notes`) cobrindo tudo acima, i18n EN + PT-BR, testes unitários em todo módulo de `lib/`. |
| **Testes** | Unitários de domínio (`Modules.Notes.Tests`: page, hierarquia, grafo, busca, slugger, tag name, tag parser, wikilink parser) + integração (`IntegrationTests/Modules/Notes`: pages, anexos, backlinks, busca, grafo, tags). |

## Ainda não implementado (previsto / planejado)

| Área | Status |
|---|---|
| **Filtros de tag na URL** | Não implementado. Os filtros são estado de componente, então "as pages com #x" não é um link compartilhável. O lugar seria um search param na `NotesPage`. |
| **Ideias de navegação no grafo** | Visão geral das pontes entre famílias e, depois, persistência do layout/comunidades automáticas. Ver [Ideias para o grafo](graph-view.md#8-ideias-de-melhorias-2026-11-02). |
| **Alinhamento de coluna pela UI** | Deliberadamente fora. O lugar seria a linha separadora em `lib/markdownTables.ts`. |
| **Backend de bucket e vídeo** | Previsto, sem data: não entra na fila atual. Só a abstração existe. `storage_backend` + `storage_key` em cada linha de mídia fazem com que plugar um bucket não exija migration; vídeo pede também download por pedaços e a versão de capa. Ver [Biblioteca de mídia](media-library.md#3-tipos-e-o-que-fica-pronto-para-vídeo). |
| **Histórico de versões** | Implementado — ver [Histórico de versões](version-history.md). |

## Deliberadamente fora (não é dívida)

- **Renderizar `![[embeds]]` inline** — decidido contra em 2026-11-02: uma page nunca é desenhada dentro
  de outra. `![[x]]` continua link; o tipo de aresta `embed` continua existindo (menções, tracejado no grafo).
- **Capa, sumário, templates de page e link público/compartilhamento** — fora por enquanto (decidido em
  2026-11-02). Saíram da lista da reforma; voltam só se o usuário pedir.
- **Preview ao vivo dentro do CodeMirror** — substituído pelo editor visual; o CodeMirror fica como modo
  Markdown cru.

- **Rename global de tag** — exigiria reescrever o markdown de toda page que a menciona; é find &
  replace, não operação de banco.
- **Rollup de tag aninhada** — `#projeto/pandora` é aceito como texto da tag, mas `#projeto` não
  inclui as filhas.
- **Endpoints de criar/deletar tag** — o texto cria a tag, a varredura remove.
- **`ON DELETE CASCADE` na árvore de pages** — o comando de delete percorre a subárvore por conta
  própria para o soft delete manter a história.

## Histórico de versões

Implementado: snapshots completos e imutáveis de cada save com alterações, sem expiração.
Agrupamento apenas visual, comparação de qualquer par (Markdown e metadados), prévia e restauração
reversível com validação de dono, hierarquia e concorrência. Páginas existentes recebem uma versão
inicial; soft delete preserva o histórico. Ver [Histórico de versões](version-history.md).

## Pontos em aberto conhecidos

1. **Retenção dos blobs.** Os bytes da mídia moram em `bytea`; revisitar mover para object storage
   se o caderno começar a carregar arquivos pesados — é também o gatilho do backend S3.
2. **Bytes sem uso.** Excluir um item mantém os bytes, porque notas podem ainda mostrá-lo; nada varre os
   bytes de um item excluído que nenhuma nota usa mais. Fica assim por decisão (2026-11-02): o lugar
   "Sem uso" da biblioteca já mostra esses itens, e quem decide apagar é o usuário.
3. **Passeio do grafo em memória.** Carregar o conjunto inteiro de pages/arestas do usuário é o certo
   para um caderno pessoal; é a primeira coisa a revisitar se o grafo crescer.
4. **Parsers duplicados.** `lib/wikilinks.ts` e `lib/tags.ts` espelham o backend de propósito, para o
   preview resolver o que o save vai resolver. Qualquer mudança numa regra de parse tem que pousar nos
   dois lados — os testes unitários de cada lado são a proteção.

## B10: modelos de página

Construído, sem commit. Marcar/desmarcar no menu da página, seção Modelos na lateral e criação a
partir de qualquer modelo ativo do usuário no espaço/pasta atual. A cópia mantém conteúdo e ícone,
recebe IDs novos em todos os níveis, remove âncoras de comentários e expande `{{date}}` (data ISO no
fuso da conta) e `{{title}}` (título final) nos textos visíveis. O título aceita `{{date}}`.
Subpáginas e configurações não são copiadas; `is_template` não é versionado. Notas diárias e tabela
de propriedades da pasta pendentes; o arrastar foi revisado abaixo. O CREATE
original ganha `is_template`; banco local não recriado nem migrado. Validação no navegador pendente.

## B10: notas diárias

Construído, sem commit. Hoje na lateral do Notes e na Home abre ou cria a nota diária da conta.
`POST /notes/daily { date? }` aceita data de calendário; ausente usa hoje no fuso efetivo da conta.
Páginas novas recebem título AAAA-MM-DD na pasta Diário (Daily em inglês) do espaço padrão. Uma
pasta raiz com esse nome é reutilizada inicialmente; o ID salvo mantém a identidade após renomear.
Pasta ausente/excluída é substituída.

A identidade do dia fica separada de título/localização em `nte017_daily_note`, única por usuário e
data. Renomear/mover/arquivar/bloquear mantém a página; reabrir não edita nem cria versão. Página na
lixeira ou eliminada é substituída no próximo acesso; restaurar a antiga não troca a nota diária
atual. Páginas comuns com título igual à data não são reutilizadas. Conflitos de slug recebem sufixo
numérico.

`nte016_notes_preferences` guarda modelo opcional e ID da pasta no próprio Notes. Configurações do
diário na lateral seleciona modelo marcado, ativo e do usuário; null limpa a escolha. Somente novas
notas usam o modelo: IDs aninhados novos, sem âncoras de comentários, cópia de ícone/conteúdo e
variáveis de data/título usando a data escolhida da nota. Modelo indisponível resulta em página
vazia. Entradas existentes preservadas. Páginas novas mantêm histórico/busca/tags/links/mídia/datas
dos blocos.

Lock consultivo de sessão PostgreSQL por usuário cobre leitura até commit atômico da unidade de
trabalho, serializando primeiros acessos e alterações de preferência entre instâncias. Liberação
explícita na mesma conexão aberta. Unicidade no banco garante um registro por usuário/data. Relações
EF para links e tags agora declaram a página para ordenar a inserção quando a pasta também é nova.

Permissões: abrir exige notes.pages.read/create, notes.folders.create e notes.spaces.read.
Preferências usam notes.preferences.read/update; selecionar modelo exige também notes.pages.read.
Vinculadas à feature Notes. Migrações novas criam nte016 e nte017; banco local não recriado nem
migrado. Navegador pendente.

## B10: revisão do arrastar na árvore

Construído, sem commit. Arrastar na lateral move/reordena páginas e pastas por uma chamada atômica
`POST /notes/tree/move`. Pastas continuam antes de páginas; soltar sobre pasta coloca dentro dela,
sobre página cria subpágina e intervalos reordenam irmãos. Ciclos e pastas dentro de páginas são
recusados. Só os irmãos visíveis da árvore principal são renumerados; modelos continuam separados,
sem arraste nem reordenação implícita.

Arraste disponível em um espaço selecionado, sem filtro de tags, conforme permissão de atualização
do tipo de item. Todos os espaços e filtro de tags não representam hierarquia completa para
reordenar. Novos movimentos ficam bloqueados até terminar gravação e recarga; destino expande após
sucesso. Destinos inválidos são bloqueados antes de soltar. Erros recarregam a árvore e permitem
tentar de novo. O menu de mover para outro espaço conserva a rota de página individual.

Backend valida dono, espaço, destino, IDs distintos e ciclos de todo o lote antes de alterar
entidades. Usa Page.Move/Folder.Move existentes e um único commit, com histórico das páginas. Lock
por usuário serializa lotes; falha não move irmãos parcialmente nem cria versões. A implementação
não despacha comandos individuais de movimento porque gravações aninhadas independentes não
garantiriam atomicidade. Sem mudança de esquema ou migração nova. Arraste nativo com mouse/toque
ainda não conferido no navegador; testes de UI cobrem o contrato de eventos Tree e envio à API.

## B10: tabela de propriedades da pasta, somente leitura

Construído, sem commit. A pasta oferece Tabela junto das visões Lista/Cartões existentes. Padrão
anterior preservado; selecionar Tabela usa a mesma preferência local de visualização. Propriedades
carregam apenas nessa visão. Cada página diretamente na pasta ganha linha, inclusive sem
propriedades; subpastas e subpáginas ficam de fora. Respeita filtros de arquivadas e interseção de
tags do Notes. Clicar no título abre a página; células somente leitura e valores ausentes/vazios
mostram travessão.

`GET /notes/folders/{id}/properties` verifica dono da pasta e permissões de leitura de
pastas/páginas. Lê apenas páginas diretamente na pasta, não excluídas, retornando resumos, união dos
nomes de propriedades e valores em texto, sem documentos completos. `PageContent.Properties()`
percorre blocos properties em qualquer nível, aparando nomes/valores, ignorando linhas sem nome e
lendo inlines de texto/status/data/item/fórmula. Variações de maiúsculas/minúsculas usam o primeiro
rótulo encontrado; chaves repetidas na mesma página preservam valores separados por quebra de linha.
Colunas seguem a ordem de encontro e páginas seguem order_index/id. Não grava conteúdo, versões ou
dados derivados.

Strings renderizadas como texto, rolagem horizontal quando necessário e estados de vazio/erro
explícitos. Trocar pasta limpa resultados anteriores. Chaves de consulta ficam sob a família de
páginas: criar/mover/arquivar/excluir invalida a projeção; autosave também atualiza as projeções
explicitamente. Sem tabela, coluna ou migração nova. Navegador e aplicação das migrações anteriores
no banco local pendentes.

Implementação da B10 concluída no escopo somente leitura aprovado. Edição nas células não incluída.
B11 integrações é a próxima; revisão visual continua pendente.

## B11: e-mail para Notes

Construído, sem commit. Salvar no Notes no leitor de e-mail abre modal com título, espaço ativo e
pasta existente (inclusive aninhada), ou raiz do espaço. Assunto sugere título, limitado a 255
caracteres. Trocar espaço limpa a pasta. Salvar abre a nova página. Cada salvamento explícito cria
cópia independente; alterações posteriores no e-mail não atualizam a nota. Anexos, imagens inline e
formatação HTML não são copiados.

`POST /communications/messages/{id}/notes` lê mensagem do usuário pelo fluxo OpenMessage existente,
incluindo cache/provedor. Cliente envia apenas título/destino, nunca o corpo. Prioriza parte MIME em
texto; e-mail somente HTML usa tokenizer MimeKit: entidades decodificadas, separação de
parágrafos/listas/linhas preservada, scripts/styles/head/template/noscript/comentários descartados,
sem executar HTML ou carregar recursos remotos. Corpo vira parágrafos literais; linhas parecidas com
markdown não viram comandos ou tags.

Communications chama `INoteImporter` em Notes.Abstractions. Implementação no Notes despacha
ImportNoteCommand e CreatePageCommand normal, preservando dono do destino, busca, histórico inicial
e datas dos blocos. Sem referência a Application/Domain/Persistence de outro módulo, acesso cruzado
a tabelas ou evento de integração novo. Porta síncrona retorna ID da página já gravada para a UI.
Título/conteúdo inválido ou destino indisponível não cria página parcial. Limites normais de
tamanho/blocos da página continuam valendo.

Origem em bookmark `/mail?message={id}`. MailPage mantém mensagem selecionada nessa URL e pode
abri-la fora da lista/caixa atual. Mensagem ausente/alheia mostra erro; voltar no celular retorna à
lista. A nota copiada continua independente se o original desaparecer. Endpoint não envia e-mail,
muda flags do provedor ou importa anexos; acesso/retenção normal do cache de corpo continua
ocorrendo.

Exige communications.messages.read, notes.pages.read/create, notes.spaces.read e notes.folders.read,
com os dois módulos habilitados. Ação escondida sem acesso Notes e desabilitada até carregar o corpo
atual. Erro ao carregar destinos bloqueia salvar. Salvamento pendente bloqueia submissão
duplicada/cancelamento; falha conserva título/destino para tentar novamente. Consultas Notes
invalidadas após sucesso. Sem mudança de esquema ou migração. Navegador/provedor real pendentes;
banco local preservado. Blocos vivos de Agenda/Finanças da B11 construídos; Radar/Databases/Projects
dependem dos respectivos módulos.

## B11: bloco de Agenda com dados atuais

Construído, sem commit. `/Agenda` insere rich block agenda-list. Origem padrão Hoje usa a visão
unificada do dia no fuso da conta (eventos, tarefas e lembretes, com estados atuais). Outro modo
seleciona lista ativa e mostra somente tarefas Todo/InProgress dela. Ambos somente leitura: sem
concluir tarefa, reconhecer lembrete ou editar evento pelo Notes. Abrir na Agenda leva à tela
correspondente.

Configuração salva apenas `{ mode: "today" }` ou `{ mode: "tasks", listId }`; props vazias usam
today. Títulos, nomes de listas, estados, datas e resultados consultados não entram no bloco,
autosave, texto de busca ou snapshots. Trocar modo limpa lista anterior. Lista não configurada,
excluída, alheia ou arquivada mostra estado explícito, sem carregar todas as tarefas como fallback.
Valida ID entre listas ativas do usuário antes de montar consulta de tarefas.

Reutiliza hooks, services e famílias de query keys da Agenda. Mutações normais da Agenda atualizam
blocos ativos pelo mesmo cache; Atualizar invalida explicitamente a origem escolhida. Mantém regras
normais de cache/foco, sem polling ou assinatura push. Consultar/atualizar não altera nota. Datas
exibidas usam fuso da conta e formatadores existentes; decidir o dia de hoje continua no servidor.
Falha da API esconde linhas antigas e permite tentar novamente.

Visão pública retorna Conteúdo privado antes de montar consultas, mesmo com dados do dono no cache.
Nota privada exige feature Agenda e permissões da origem: Hoje exige
agenda.events.read/tasks.read/reminders.read; lista exige agenda.tasks.read/lists.read. Nota
travada/em leitura pode consultar e atualizar, mas não configurar origem/lista. Exportações
Markdown/HTML emitem somente [Agenda], sem IDs ou valores consultados; PDF pelo navegador usa a
visão do dono renderizada naquele momento. IDs/configuração sobrevivem ao editor e versões.

Sem endpoint backend, esquema, migração ou dependência backend cruzada nova. Navegador/conta real
pendentes. Próximos eventos construídos abaixo. Blocos Radar/Databases/Projects dependem dos respectivos módulos.

## B11: resumos de Finanças com dados atuais

Construído, sem commit. /Finanças insere rich block finance-summary. Conta mostra saldos
efetivado/projetado; cartão mostra limites total/disponível, distinguindo zero de limite não
definido. Origem arquivada, excluída ou de outro dono aparece indisponível. O ID deve estar na
lista ativa do usuário antes da consulta de valores. Nenhuma origem é selecionada automaticamente.

Despesas mensais agregam todas as páginas dos lançamentos kind=expense, status=posted, pela data
do lançamento (from/to inclusivos). Total bruto: reembolsos não são descontados;
pendentes/anulados, receitas, transferências e pagamentos de fatura ficam fora. Moedas permanecem
separadas. Categorias agrupam por identidade, incluindo nomes históricos inativos; categoria
indisponível difere de despesa sem categoria. Erro em lançamentos/categorias esconde o resultado
para evitar total parcial.

Salva somente configuração: { mode: "account", accountId }, { mode: "card", cardId } ou { mode:
"expenses", month?: "YYYY-MM" }. Props vazias usam conta. Sem month, resolve o mês atual no fuso
da conta; mês fixo usa datas de calendário, incluindo bissextos. Mês inválido não consulta
valores. Trocar modo limpa origem/mês anterior. Nomes, saldos e totais não entram em persistência,
autosave ou versões/templates.

Frontend privado reutiliza hooks/services/query keys de Finanças. Mutações normais invalidam
resumos ativos; Atualizar recarrega a origem selecionada. Cache/foco normais, sem polling.
Consultar/atualizar não altera nota. Nota travada/em leitura pode consultar e atualizar, mas não
configurar.

Exige feature Finanças e permissões da origem: finances.accounts.read, finances.cards.read ou
ambas finances.transactions.read e finances.categories.read. Página pública retorna Conteúdo
privado antes de montar consultas, mesmo com cache do dono. Markdown/HTML emitem apenas
[Finances]; PDF usa a visão do dono renderizada no momento. Sem rota backend, dependência, esquema
ou migração nova. Navegador/conta real pendentes.

Verificação: 893 testes frontend passaram (34 novos para agregação, meses, paginação, acesso,
público, configuração, atualização e invalidação por mutação). Build/typecheck, ESLint Notes e
antd lint passaram. Backend sem alteração; suítes não rerodadas. Navegador/conta real pendentes.
Sem commit ou alteração no banco local.

## B11: próximos eventos da Agenda

Construído, sem commit. O bloco /Agenda oferece Próximos eventos. Configuração: { mode: "events",
days: 7 | 30, calendarId?: string }; days ausente/não suportado usa 7. Sem calendarId, considera
todos os calendários ativos, independentemente da visibilidade na tela Calendário. ID configurado
deve estar na lista ativa do dono. Calendário ausente/arquivado/de outro dono fica indisponível,
sem cair em todos. Lista ativa vazia não consulta eventos.

Janela cobre hoje mais as próximas 6 ou 29 datas no fuso da conta. Cada meia-noite local é
convertida separadamente para UTC, respeitando horário de verão. Fim exclusivo: meia-noite
seguinte à última data. Mostra eventos em andamento e futuros em ordem de início; oculta
encerrados/cancelados e início no/após limite final. Eventos de dia inteiro/vários dias usam
formatador existente da Agenda, incluindo fim exclusivo. Janela resolve ao renderizar/reabrir e
Atualizar; sem polling ou timer de meia-noite.

Reutiliza useCalendars/useEventOccurrences e query keys da Agenda. Endpoint existente expande
recorrências e aplica edições/cancelamentos de ocorrência; Notes não reimplementa recorrência nem
consulta detalhe de série. Filtro envia chaves calendarIds repetidas para binding ASP.NET.
Mutações normais de eventos/calendários invalidam blocos ativos; Atualizar recarrega ambas as
famílias. Erro esconde resultados antigos e permite tentar novamente.

Exige feature Agenda, agenda.events.read e agenda.calendars.read. Nota travada/em leitura pode
consultar/atualizar, mas não configurar. Trocar origem limpa filtros. Persiste apenas dias/ID;
nomes, ocorrências e datas resolvidas não entram em autosave, histórico ou templates. Página
pública bloqueia consultas mesmo com cache do dono. Markdown/HTML mantêm [Agenda]; PDF usa a visão
do dono renderizada. Sem mudança backend, esquema ou migração.

Verificação: 909 testes frontend passaram, incluindo 16 casos novos para limites no fuso/horário
de verão, ocorrências expandidas, eventos em andamento/dia inteiro, fim exclusivo,
acesso/privacidade, configuração e invalidação por mutação. Build/typecheck, ESLint Notes e
service Agenda alterado, e antd lint Notes passaram. Backend sem alteração; suítes não rerodadas.
Navegador/conta real pendentes. Sem commit ou alteração no banco local.
