# Biblioteca de mídia

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Editor](editor.md), [Referência de API](api-reference.md)

---

## 1. O que é

A **biblioteca** guarda as imagens e os arquivos que as notas usam, organizados em **pastas
virtuais** — nomes numa árvore, nada no disco. Ela é do usuário e compartilhada por todos os
[espaços](spaces.md): uma imagem pode aparecer em notas de espaços diferentes.

Colar ou arrastar um arquivo no editor manda ele para a biblioteca, em **"Não organizadas"** (sem
pasta), e insere o markdown que o embeda. Na tela `/notes/library` dá para organizar tudo depois.

## 2. As tabelas

| Tabela | O que guarda |
|---|---|
| `nte002_media` | O item: tipo (`kind`), nome, nome original, content type, tamanho, dimensões, pasta e o par `(storage_backend, storage_key)` que localiza os bytes |
| `nte008_media_folder` | As pastas, uma árvore por `parent_id` |
| `nte009_media_rendition` | Versões derivadas de um item, com bytes próprios — hoje a miniatura |
| `nte003_file_blob` | Os bytes, tanto dos itens quanto das versões, no backend `DatabaseFileStorage` |

`name` é o que a biblioteca mostra e pode ser renomeado; `original_file_name` continua sendo o nome do
arquivo baixado.

## 3. Tipos e o que fica pronto para vídeo

`kind` é `image`, `file` ou `video` (`MediaKind`). O upload decide:

- um content type `image/*` que o servidor consegue ler vira `image`, com largura e altura;
- uma imagem que o servidor não lê (formato desconhecido, bytes quebrados) vira `file` — guardada, só
  não desenhada;
- qualquer outra coisa vira `file`;
- `video/*` é recusado (`Media.UnsupportedKind`, 422): `MediaKind.Accepted` ainda não tem vídeo.

O vídeo já tem lugar no schema e no código. Aceitá-lo pede, além de trocar aquela constante: um
backend de bucket (o `storage_backend` de cada linha já permite, sem migration), download por
pedaços (HTTP 206) e um `purpose` novo em `nte009` para a capa. A tela já escolhe como mostrar pelo
`kind`.

## 4. Miniaturas

No upload, uma imagem com o lado maior acima de **400 px** ganha uma miniatura WebP de até 400 px
(`RenditionPurpose.Thumbnail`), gravada como uma linha de `nte009` com bytes próprios no blob store.
Uma imagem menor que isso não ganha: ela mesma já serve de miniatura, e a tela usa o original quando
`thumbnailUrl` vem nulo.

A geração mora atrás do port `IImageProcessor`, implementado com **ImageSharp 3.1**
(`Infrastructure/Images/ImageSharpProcessor.cs`):

- **Rotação EXIF.** Foto de celular costuma guardar o "gire 90°" nos metadados, não nos pixels. As
  dimensões e a miniatura já saem de pé.
- **Por que 3.1 e não 4.x.** A 4.x verifica uma chave de licença no build e **falha o build em
  Release** sem ela. A 3.1 tem a mesma licença (Six Labors Split: Apache 2.0 abaixo de US$ 1 milhão
  de receita) sem essa verificação. É código .NET puro — nada nativo na imagem Docker.

## 5. Pastas

Criar, renomear, mover e excluir, numa árvore como a das páginas. Mover para dentro de si mesma ou
de uma subpasta é recusado (`Media.FolderCycle`, 409), com o mesmo passeio para cima que a árvore de
páginas usa (`PageHierarchy.WouldCreateCycle`).

**Excluir uma pasta não exclui nada dentro dela**: as subpastas e os itens sobem para a pasta de
cima (ou para "Não organizadas"/o topo) antes de a pasta sair. As FKs `fk_nte002_folder` e
`fk_nte008_parent` estão declaradas no EF para ele ordenar essas atualizações antes do delete.

## 6. Excluir um item

A exclusão é soft (`deleted_at`): o item some da biblioteca, mas **os bytes continuam servidos**, porque
uma nota pode ainda mostrá-lo (ver §10). Por isso o download não filtra itens excluídos.

## 7. Servir os bytes

`GET /notes/media/{id}` e `GET /notes/media/{id}/thumbnail` são autenticados. O token mora no
`localStorage`, não em cookie, então nem `<img src>` nem um clique chegam neles sozinhos: o cliente
busca o blob pelo `apiClient` (`lib/api/blob.ts`, compartilhado com o Communications) e usa um object
URL.

As respostas saem com **`Cache-Control: private, max-age=31536000, immutable`**: um id aponta para
bytes que nunca mudam. O navegador guarda tanto a imagem quanto a miniatura, inclusive entre
sessões — é isso que mantém barata uma grade com dezenas de miniaturas, sem precisar de link
assinado.

O preview das notas continua resolvendo as imagens embedadas antes de renderizar
(`hooks/useMediaUrls.ts`), trocando o caminho pelo object URL **depois** do DOMPurify, cuja
allow-list de URI não cobre `blob:`. Um clique num link de arquivo vira um download no lugar, sem
navegar a aba para a API.

## 8. A tela

`/notes/library` (`pages/LibraryPage.tsx`):

- **À esquerda**, "Tudo", "Não organizadas" e a árvore de pastas, cada um com quantos itens tem. Cada
  pasta tem "+" (subpasta) e "⋯" (renomear, mover, excluir).
- **No centro**, a grade de miniaturas, com busca por nome (que procura na biblioteca inteira), filtro
  Imagens / Arquivos e envio por botão ou arrastando arquivos do computador — para a pasta aberta.
- **Arrastar um item** para uma pasta (ou para "Não organizadas") move ele.
- **Clicar num item** abre os detalhes: prévia, nome editável, pasta, baixar, copiar o markdown,
  excluir, e tipo, tamanho, dimensões, nome original e data.
- A pasta aberta fica na URL (`?folder=`), e o menu do app e a barra lateral das notas levam até ela.

## 9. Mídia dentro das notas

**Inserir.** `/imagem` e `/arquivo` no menu de barra, ou o botão de imagem na barra da página, abrem
o seletor da biblioteca (`components/library/MediaPickerModal.tsx`): pastas, busca, grade e
"Enviar e inserir", que manda o arquivo para a pasta aberta e já insere. O que entra é markdown
comum — `![nome](…)` para imagem, `[nome](…)` para arquivo — no cursor.

**Largura e legenda** vêm do texto alternativo, no estilo do Obsidian (`lib/mediaMarkdown.ts`):

```
![Praia de Jurerê|360](/api/v1/notes/media/…)
```

- O número depois da `|` é a largura em pixels; sem ele, a imagem vai até a largura da coluna.
- Um alinhamento também pode vir, em qualquer ordem com a largura: `esquerda`/`left` e
  `direita`/`right` flutuam a imagem com o texto correndo ao lado (no celular não), `centro`/`center`
  centraliza — `![Praia de Jurerê|360|direita](…)`.
- O texto antes vira legenda, **exceto quando parece um nome de arquivo** (`IMG_2034.jpg`) — o que
  colar escreve —, para as imagens antigas não ganharem uma legenda que ninguém escolheu.
- Clicar na imagem abre em tela cheia (o preview do `Image` do antd).
- Um `<img src="/api/v1/notes/media/…">` escrito em HTML carrega do mesmo jeito, para quando precisa
  de um atributo que o texto alternativo não diz; ele também conta como uso do item.

**Arquivos** viram um cartão com ícone, nome e tamanho; clicar baixa com o nome original. O tamanho
vem de `GET /notes/media/lookup`, que responde vários ids de uma vez, excluídos incluídos (marcados
"removido da biblioteca").

## 10. Quais notas usam cada item

`nte010_page_media` é derivada do markdown a cada save, como as arestas de wikilink
(`PageMediaSynchronizer`, `MediaReferenceParser`): todo caminho `/api/v1/notes/media/{id}` fora de
código, de um item do próprio usuário, vira uma linha. Tirar a imagem do texto tira a linha; excluir a
página tira todas as dela.

Com isso a biblioteca mostra, nos detalhes, **"Usado em N notas"** com links para elas; tem o lugar
**"Sem uso"** para limpar o que nenhuma nota mostra; e **excluir um item em uso avisa** listando as
notas — sem bloquear, porque elas continuam mostrando o item.
