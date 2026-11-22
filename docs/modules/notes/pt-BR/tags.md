# Tags

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Busca](search.md), [Grafo](graph-view.md)

---

## 1. O modelo: o bloco de tags da página manda, a linha guarda a cor

As tags de uma página ficam num **bloco `tags`**, o primeiro bloco do documento:
`{ "type": "tags", "props": { "tags": ["dotnet", "Mensageria"] } }`. O cabeçalho as edita como chips;
o editor as salva como esse bloco.

Acima disso fica uma linha `Tag` (`nte005`) com o que o documento não diz: a **cor**, e o nome como foi
escrito da primeira vez. Não há CRUD para ela: a tag nasce quando uma página a carrega pela primeira
vez.

> Consequência aceita: **não existe renomear tag em todas as páginas.** Renomear seria reescrever toda
> página que a carrega.

## 2. Normalização

O `TagName.ToSlug` transforma um nome na identidade: minúsculas, sem acentos, mantendo `/`, `-` e `_`,
no máximo 50 caracteres, e pelo menos uma letra (`123` não é tag). `Café` e `cafe` são a mesma tag; o
**nome** exibido é como foi escrita primeiro.

## 3. Sincronização e a varredura de órfãs

A cada salvamento o `PageTagSynchronizer` lê o bloco de tags (em qualquer profundidade), resolve as
tags pelo slug e **compara** com a `nte006_page_tag`, com dois acréscimos que o grafo de links não
precisa:

- ele **cria** as tags que a página acabou de trazer;
- ele **varre** as tags cuja última página acabou de largá-las, *a menos que tenham cor*. A cor é a
  única coisa que vale manter uma linha vazia viva.

A varredura desconta as arestas removidas na mesma transação, recebendo o id da página que está sendo
salva. Excluir uma página roda o `ClearAsync`, que tira todas as linhas dela e varre o que ficou órfão.

O EF conhece a chave estrangeira `page_tag → tag` mesmo sem propriedade de navegação, então apaga as
linhas de uma tag órfã antes da tag.

## 4. Cor

`PUT /notes/tags/{id}/color` é a única escrita. Só hex é aceito (`#rgb`, `#rrggbb`, `#rrggbbaa`),
porque o valor vai inline no estilo do chip; qualquer outra coisa responde **422**.

## 5. Filtros

O filtro é um parâmetro `tagIds`, e uma regra vale em toda superfície: **várias tags se intersectam
(E)**. O `TagFilter.MatchingPageIdsAsync` é a implementação única; ele devolve `null` quando nenhuma
tag foi pedida, o que significa *sem filtro*.

| Superfície | Comportamento com filtro |
|---|---|
| **Barra lateral** (`GET /notes/pages`) | Uma lista plana em vez de árvore; o arrasto fica desligado. |
| **Busca** (`GET /notes/pages/search`) | Filtra antes do ranking e do limite de 20. Com `q` vazio, lista as páginas das tags. |
| **Grafo** (`/graph`, `/{id}/graph`) | Os nós são cortados antes da caminhada pela vizinhança. |
| **Página** | O cabeçalho mostra as tags da página; clicar numa filtra a barra lateral. |
| **Bloco páginas por tag** | `tag-pages` lista as páginas que têm as tags dele. |

Os filtros ficam no estado do componente e não entram na URL.

## 6. Fora do escopo

- **Renomear em todas as páginas** (§1).
- **Hierarquia de tags:** `projeto/pandora` é uma tag; `projeto` não a inclui.
- **Endpoints de criar/apagar tag:** as páginas criam, a varredura remove.
