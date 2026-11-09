# Páginas e hierarquia

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Espaços](spaces.md), [Blocos e editor](blocks.md)

---

## 1. Páginas, pastas e subpáginas

- Uma **página** (`nte001`) tem um [documento de blocos](blocks.md), título, ícone emoji, slug e seu
  lugar na árvore.
- Uma **pasta** (`nte012`) contém pastas e páginas e fica num [espaço](spaces.md). Tem nome, ícone e
  descrição, editados no lugar na página dela (`/notes/f/:id`).
- Uma **página de topo** fica numa pasta (`folder_id`) ou no topo do espaço. Uma **subpágina** fica sob
  a página-pai (`parent_id`) e mora no espaço dela.
- **Nunca uma pasta dentro de uma página.**

Os irmãos são ordenados por `order_index`; pastas vêm antes de páginas. O frontend aninha as listas
planas de `GET /notes/pages` e `GET /notes/folders` com `lib/buildTree.ts`.

**Ciclos são recusados.** `PageHierarchy.WouldCreateCycle` sobe a partir do pai pretendido e acusa um
ciclo se chegar à página que está sendo movida; pastas seguem a mesma regra.

## 2. A barra lateral

De cima para baixo (`components/NotesSidebar.tsx`):

1. **O seletor de espaço**, a busca (`Ctrl+K`) e o grafo.
2. **Atalhos:** a **Visão geral** do espaço, **Hoje** (a nota diária, com a engrenagem das
   configurações) e **Recentes** (as últimas cinco páginas abertas).
3. **A árvore:** pastas e páginas juntas. O `+` de uma pasta cria uma página ou uma pasta nela.
4. **Modelos**, numa seção própria.

**Arrastar e soltar** passa por uma requisição atômica, `POST /notes/tree/move`: soltar numa pasta
coloca o item dentro dela, numa página cria uma subpágina, e os vãos reordenam os irmãos. O backend
confere dono, espaço, destino e ciclos do lote inteiro antes de mover qualquer coisa, então um arrasto
que falha não move nada nem grava versão. Arrastar só funciona num espaço selecionado e sem filtro de
tag, porque as outras visões não mostram a hierarquia completa. "Mover para outro espaço" no menu da
página usa o `POST /notes/pages/{id}/move` de uma página só.

Um **filtro de tag** transforma a árvore numa lista plana e desliga o arrasto.

## 3. Visão geral, página da pasta e página

- **Visão geral do espaço** (`/notes/s/:spaceId`): contagens, páginas editadas recentemente, e a
  estrutura em cartões ou em árvore.
- **Página da pasta** (`/notes/f/:id`): o conteúdo em lista, cartões ou **tabela**. A tabela tem uma
  linha por página direta da pasta e uma coluna por propriedade achada nos blocos de propriedades
  delas (`GET /notes/folders/{id}/properties`). É somente leitura.
- **Página** (`/notes/:id`): trilha (espaço, pastas, pais), cabeçalho (ícone, título, tags), o
  documento, depois as subpáginas e "Mencionada em".

## 4. Slugs

O `Slugger` deriva o slug do título (minúsculas, sem acentos, não alfanuméricos viram hífen, no máximo
80 caracteres, `untitled` quando não sobra nada). O `CreatePage` acrescenta `-2`, `-3`, … até ficar
livre. O slug é **fixo na criação**; uma página na lixeira libera o dela, porque o índice único cobre só
páginas vivas.

## 5. Estados

| Estado | Onde fica | Significado |
|---|---|---|
| **Favorita** | `is_favorite` | Uma marca para a barra lateral. |
| **Arquivada** | `archived_at` | Some da árvore padrão, continua editável, aparece na busca (marcada) e no grafo quando "Mostrar arquivadas" está ligado. |
| **Modelo** | `is_template` | Listada em Modelos; ver §7. |
| **Na lixeira** | `deleted_at` | Ver §6. |

## 6. Lixeira

- **Excluir** uma página carimba um mesmo momento nela e em todas as subpáginas, para voltarem juntas.
  Excluir uma pasta leva junto as subpastas e as páginas.
- **Restaurar** (`POST /notes/trash/{id}/restore`) devolve a página para baixo do pai, ou para o topo
  do espaço na pasta dela quando o pai sumiu, ou para o espaço padrão quando o espaço sumiu. Se uma
  página viva pegou o slug nesse meio-tempo, a restaurada ganha um slug numerado. Links, usos de mídia
  e tags são recalculados.
- **Excluir de vez**, uma página ou a lixeira inteira, remove as linhas, as versões, os comentários, as
  datas dos blocos e todo link de ou para elas.

## 7. Modelos

- Marcar ou desmarcar uma página no menu de opções (`PUT /notes/pages/{id}/template`). O
  `is_template` não entra nas versões.
- **Nova a partir de modelo** (`POST /notes/pages/from-template`) copia o conteúdo e o ícone para o
  espaço e a pasta escolhidos, com ids novos em todo bloco, sem âncoras de comentário, e com `{{date}}`
  (a data local da conta) e `{{title}}` expandidos no texto visível. O título aceita `{{date}}`.
- Subpáginas e configurações da página não são copiadas.

## 8. Configurações da página

| Configuração | Como funciona |
|---|---|
| **Capa** | Uma imagem da biblioteca (que dá para subir e descer) ou um de seis gradientes. Uma capa da biblioteca conta como uso do item. |
| **Largura, fonte, texto pequeno** | `appearance.width` (`normal`, `wide`), `font` (`default`, `serif`, `mono`), `smallText`. |
| **Trava** | Uma página travada abre somente leitura em toda tela; a etiqueta "Travada" no cabeçalho destrava. |
| **Verificação** | Verificada por 7, 30, 90 ou 365 dias, ou sem validade. O cabeçalho mostra até quando, e "vencida" quando passa. |
| **Páginas recentes** | Abrir uma página carimba `viewed_at` direto no banco. Não é edição: nem o "editado" nem as versões mudam. |
| **Link público** | Um token aleatório de 32 bytes. `/p/<token>` lê a página sem login. Só os itens da biblioteca que a página usa são servidos pelo link, uma página mencionada aparece como "Página privada" e os blocos ao vivo mostram "Conteúdo privado". Desligar e ligar o link gera um novo. |

Nenhuma delas faz parte das versões da página.

## 9. Notas diárias

**Hoje** (na barra lateral e na Home) abre ou cria a página de uma data
(`POST /notes/daily { date? }`; sem data é hoje no fuso da conta).

- Uma página diária nova tem o título `YYYY-MM-DD` e vai para a **pasta diária**: uma pasta de topo
  chamada "Diário" ("Daily" em inglês) no espaço padrão. Na primeira vez, uma pasta de topo existente
  com esse nome é adotada; o id dela fica então em `nte016.daily_folder_id`, e renomeá-la a mantém em
  uso. Uma pasta que sumiu é criada de novo.
- O **modelo diário** (`nte016.daily_template_id`, escolhido na engrenagem ao lado de Hoje) preenche as
  páginas novas, com `{{date}}` e `{{title}}` expandidos para a data da nota. Um modelo indisponível
  gera uma página vazia; páginas existentes nunca são tocadas.
- Que página pertence a que data fica em `nte017`, não é lido do título: renomear, mover, arquivar ou
  travar mantém a ligação. Uma página na lixeira ganha substituta na próxima visita.
- Uma trava consultiva por usuário serializa as primeiras visitas, então duas abas nunca criam duas
  páginas para a mesma data.

## 10. Exportar e importar

- **Exportar** pelo menu da página: Markdown, um arquivo HTML autossuficiente (com as imagens da
  biblioteca dentro, como data URLs), ou PDF pela janela de impressão do navegador, com uma folha de
  estilo que deixa só a página.
- **Importar** um arquivo Markdown pelo menu `+`: o primeiro título vira o título da página, e
  `[[Título]]` vira uma menção a uma página que existe.

## 11. Commands e queries

| Caso de uso | Notas |
|---|---|
| `CreatePage`, `UpdatePage` | Validam o documento e reconstroem links, tags, usos de mídia e datas dos blocos. `UpdatePage` recebe `source: edit \| assistant`. |
| `MovePage`, `MoveTreeItems` | Uma página (também entre espaços, com as subpáginas), ou um lote vindo da barra lateral. |
| `SetPageFavorite`, `SetPageArchived`, `SetPageTemplate` | Marcas. |
| Commands de `PageSettings` | Aparência, trava, verificação, link público, acesso. |
| `DeletePage`, commands de `Trash` | Lixeira, restaurar, excluir de vez, esvaziar. |
| `CreatePageFromTemplate`, `OpenDailyNote` | §7 e §9. |
| `CreateFolder`, `UpdateFolder`, `MoveFolder`, `DeleteFolder` | Pastas. |
| `GetPage`, `GetPageTree`, `GetFolders`, `GetFolderProperties`, `PageViews` | Leituras. |
