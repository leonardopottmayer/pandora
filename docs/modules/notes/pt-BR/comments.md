# Comentários e datas dos blocos

[← Voltar ao índice](README.md) · Relacionados: [Blocos e editor](blocks.md), [Histórico de versões](version-history.md)

Duas tabelas se apoiam nos ids estáveis dos blocos: `nte013_page_comment` e `nte014_block_stamp`.
Nenhuma faz parte das versões da página; excluir a página de vez remove as duas.

---

## 1. Comentar em texto ou num bloco

| Onde | Como |
|---|---|
| **Em palavras** | "Comentar" na barra de seleção. A conversa guarda o id do bloco e as palavras citadas; o editor marca as palavras com uma marca `comment` que guarda só o id da conversa, então um bloco pode ter várias conversas sobre as mesmas palavras. |
| **Num bloco inteiro** | "Comentar" no menu do bloco (a alça de arrastar). Uma conversa aberta num bloco aparece como uma linha ao lado dele. |

## 2. Conversas

- Uma conversa é o primeiro comentário; as respostas apontam para ele (`thread_id`). Uma resposta não
  pode ser respondida.
- Editar, excluir (uma conversa vai com as respostas), resolver e reabrir. Só o primeiro comentário é
  resolvido.
- A página pinta só as marcas das conversas **abertas**. Uma conversa resolvida ou excluída deixa o
  texto como está, sem mudar o documento. Clicar em palavras comentadas abre a conversa.

## 3. O painel de comentários

O botão da barra de ferramentas mostra quantas conversas estão abertas. O painel lista as abertas e as
resolvidas; uma conversa cujo bloco foi removido continua, marcada "Bloco removido"; clicar numa
citação leva ao bloco.

O app tem um usuário por página, então o autor de um comentário é sempre o dono da página.

## 4. Datas dos blocos

A cada salvamento, a **impressão digital** de cada bloco (um MD5 do próprio JSON, sem os filhos) é
comparada com a guardada (`BlockStampSynchronizer`):

- um id novo é um bloco novo: `created_at` e `updated_at` são preenchidos;
- uma impressão diferente é uma edição: `updated_at` e `updated_source` mudam;
- um bloco sem mudança mantém o carimbo.

`updated_source` é `edit`, `assistant` (um resultado de IA aceito, uma captura, uma ferramenta do
Assistant) ou `restore`. O menu do bloco mostra quando ele foi escrito e editado pela última vez, e
por quê.

## 5. API

| Método | Rota | Para quê |
|---|---|---|
| GET | `/notes/pages/{id}/comments` | Todas as conversas da página com as respostas |
| POST | `/notes/pages/{id}/comments` | Abrir uma conversa: `{ blockId, quote?, body }` |
| POST | `/notes/comments/{id}/replies` | Responder: `{ body }` |
| PUT | `/notes/comments/{id}` | Editar: `{ body }` |
| PUT | `/notes/comments/{id}/resolved` | Resolver ou reabrir: `{ resolved }` |
| DELETE | `/notes/comments/{id}` | Excluir um comentário (o primeiro leva as respostas) |
| GET | `/notes/pages/{id}/block-stamps` | As datas e a origem de cada bloco |
