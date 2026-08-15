# Anexos e storage

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Editor](editor.md), [Referência de API](api-reference.md)

---

## 1. O que é um anexo

Qualquer arquivo binário subido ao módulo: uma imagem para embedar, um PDF, um zip. Duas linhas estão
envolvidas:

- **`nte002_attachment`** — os metadados mais o par `(storage_backend, storage_key)` que localiza os
  bytes. Write-once: nada edita um anexo depois do upload, que é por isso que ele carrega só um
  `created_at`.
- **`nte003_file_blob`** — os bytes em si, no MVP uma coluna `bytea`.

O `page_id` é opcional e **não tem foreign key**: um anexo pode existir antes de ser embedado em
lugar nenhum, e uma page pode ser soft-deletada enquanto o anexo dela continua ali.

## 2. A abstração de storage

`IFileStorage` (em `Persistence/Storage`) é o port: salvar, ler e deletar um blob. Existe exatamente
uma implementação real — **`DatabaseFileStorage`**, sobre o `IFileBlobRepository`.

A abstração não é a parte interessante; as **linhas auto-descritivas** são. Cada anexo grava *qual
backend* tem os bytes dele e *a chave dentro dele*. Quando um backend S3/MinIO entrar, os uploads
novos gravam `storage_backend = S3` e uma chave de bucket, as linhas antigas continuam dizendo
`Database`, e as **leituras não precisam de migration** — a linha diz ao leitor onde olhar.

> O gatilho natural para ligar o S3 é tamanho: `bytea` é ótimo para imagens e arquivos
> pequenos/médios; vídeo pesado ou zip grande é onde a abstração se paga.

## 3. Upload e download

| Endpoint | Comportamento |
|---|---|
| `POST /notes/attachments` | `multipart/form-data`: `file` mais um `pageId` opcional. Content type vazio cai em `application/octet-stream`. Devolve `AttachmentDto`, cuja `url` é `/api/v1/notes/attachments/{id}` — exatamente o caminho que o editor escreve no markdown. |
| `GET /notes/attachments/{id}` | Serve os bytes com o `Content-Type` guardado e um `Content-Disposition: inline` carregando o nome original. |

`inline` e não `attachment` para uma imagem embedada renderizar no lugar; o nome do arquivo viaja
junto de qualquer forma, para os arquivos que o usuário escolher salvar.

Os dois endpoints são `[Authorize]`. Servir arquivo por path direto nunca esteve na mesa.

## 4. A consequência: o browser não alcança um anexo sozinho

O endpoint de download é autenticado e o token mora no `localStorage`, **não em cookie**. Então nem
uma navegação do browser nem um `<img src>` chegam nele sozinhos. Os dois caminhos que dependiam disso
estavam tratados pela metade, e os dois foram corrigidos do mesmo jeito — o cliente busca o blob pelo
`apiClient` e entrega o object URL resultante ao DOM.

**Links de anexo.** O markdown guarda um caminho relativo, então clicar na âncora navegava a aba atual
para `/api/...` **na origem do frontend** — em dev o Vite sem proxy (daí a página em branco), em
produção o nginx e um 401. O clique agora é interceptado pelo mesmo handler que já cuidava de wikilink
e tag: busca o blob e entrega a um `<a download>` descartável. Sem aba nova, sem navegação. O nome do
arquivo vem do rótulo do markdown, que é o nome original que o editor escreveu ao embedar.

**Imagens do preview.** O object URL era escrito direto no nó do DOM (`img.src = …`), fora do modelo
do React. Qualquer re-render que **não** mudasse o markdown reescrevia o HTML e restaurava o caminho
original, enquanto o efeito que buscaria de novo dependia só de `[html]`, que não tinha mudado —
imagem quebrada até a próxima tecla. Trocar de tema, trocar de idioma e qualquer refetch que recriasse
`pageIndex`/`tagIndex` (autosave, foco na janela) faziam isso.

A correção, em `hooks/useAttachmentUrls.ts`:

- Os anexos são resolvidos **antes** de renderizar, então a URL faz parte do próprio `html`. O
  `combine` do `useQueries` memoiza o mapa, então a identidade só muda quando os resultados mudam.
- A substituição acontece **depois** do DOMPurify — a allow-list de URI dele não cobre `blob:`, e um
  object URL embutido antes seria removido.
- De brinde, cada anexo é baixado **uma vez por sessão** em vez de a cada tecla digitada; o efeito
  antigo rebuscava todas as imagens a cada mudança do markdown.
- Ninguém revoga o object URL: um id endereça bytes imutáveis, então existe no máximo um por anexo por
  sessão, e o reload que encerra a sessão os libera.

O teste de regressão é o que reproduz o bug: renderiza, espera o `blob:`, e re-renderiza com
`pageIndex`/`tagIndex` novos — exatamente o que um refetch produz.
