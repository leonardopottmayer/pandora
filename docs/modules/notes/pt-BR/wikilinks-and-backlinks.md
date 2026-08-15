# Wikilinks e backlinks

[← Voltar ao índice](README.md) · Relacionados: [Visão em grafo](graph-view.md), [Modelo de dados](data-model.md), [Editor](editor.md)

O grafo wiki é **derivado do markdown**, não cadastrado. Ele é também a fundação que a
[visão em grafo](graph-view.md) desenha.

---

## 1. Sintaxe

| Escrito | Significado |
|---|---|
| `[[Alvo]]` | Um wikilink para a page cujo **título** ou **slug** é `Alvo`. |
| `[[Alvo\|apelido]]` | O mesmo link, exibido como `apelido`. O alvo termina no pipe. |
| `![[Alvo]]` | Um **embed** — um tipo de aresta separado. |

O `WikilinkParser` devolve cada referência uma vez por par (alvo, tipo): um alvo linkado cinco vezes
na mesma page é um fato só, mas uma page que linka e embeda outra produz duas arestas.

## 2. Resolvendo o alvo

O `PageLinkSynchronizer` resolve contra as pages do dono, **título primeiro (case-insensitive), depois
slug**. As duas grafias são consultadas numa ida só ao banco, então `[[Meeting Notes]]` e
`[[meeting-notes]]` colapsam na mesma aresta.

Uma referência que não casa com page nenhuma **não produz aresta**. Um link quebrado existe só no
texto — o grafo nunca contém aresta apontando para o nada.

## 3. Reconstruindo as arestas

O parse roda no **update e no create** (uma page pode nascer com conteúdo). A reconstrução é um
**diff**, não um apaga-e-recria:

1. calcular o conjunto que o corpo pede, como pares `(targetId, kind)`;
2. remover as arestas guardadas que não são mais desejadas;
3. inserir só as genuinamente novas.

Re-salvar o mesmo texto não toca em nada — e, o que importa, a mesma linha nunca é apagada e
reinserida dentro de uma transação, o que o `uq_nte004_edge` rejeitaria.

## 4. Semântica de deleção

Soft-deletar uma page remove as arestas que **saem** dela. As que apontam **para** ela ficam na tabela
e são filtradas na leitura, o que significa que restaurar a linha restauraria as menções recebidas. É
essa mesma filtragem que garante que o payload do grafo nunca carregue aresta para um nó que não está
lá.

## 5. Backlinks ("linked mentions")

`GET /notes/pages/{id}/backlinks` devolve as pages que referenciam esta, como `BacklinkDto(PageId,
Title, Slug, Icon, IsArchived, Kind)`. Uma page que linka e embeda aparece uma vez por tipo — o mesmo
formato que o grafo usa. O `ix_nte004_target_page_id` existe para essa leitura.

O frontend mostra tudo em `components/BacklinksPanel.tsx`, ao lado do painel do grafo local.

## 6. No frontend

- `lib/wikilinks.ts` espelha o parser e o slugger do backend, para o preview resolver uma referência
  exatamente como o próximo save vai resolver. É duplicação deliberada, com testes unitários próprios.
- Um `[[alvo]]` não resolvido vira um link de **create-on-click**: clicar cria a page e navega até ela.
- Um `![[embed]]` renderiza como link comum. Renderizar o conteúdo do alvo inline **não está
  implementado** — ver [Status de implementação](implementation-status.md).
- O autocomplete de `[[` está descrito em [Editor §4](editor.md#4-três-menus-de-autocomplete). Ele
  chegou depois do trabalho de blocos ricos, que foi o que trouxe o `@codemirror/autocomplete` como
  dependência direta e lhe deu um menu ao lado de quem se registrar.
