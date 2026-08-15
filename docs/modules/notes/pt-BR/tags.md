# Tags

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Busca](search.md), [Visão em grafo](graph-view.md)

---

## 1. O modelo: híbrido — o texto manda, a linha guarda a cor

A tag é **escrita no markdown** (`#ideias`), como no Obsidian, e o backend materializa as arestas no
save — a mesma mecânica já provada pelo `PageLink`. Isso mantém a promessa central do módulo: markdown
é a fonte da verdade, e um `.md` exportado leva as tags junto.

Sobre isso mora uma linha `Tag` guardando o que o texto não sabe dizer: a **cor** e o nome como foi
escrito da primeira vez. Ela não é gerenciada por CRUD — nasce quando uma tag aparece num conteúdo, e
o texto continua no comando.

> Consequência aceita: **não há rename global de tag.** Renomear exigiria reescrever o markdown de
> todas as pages que a carregam — é `find & replace`, não uma operação de banco.

## 2. Regras de parse

`#` é muito mais comum em prosa e em shell do que `[[`, então o `TagParser` é mais rígido que o parser
de wikilink:

| Regra | Efeito |
|---|---|
| Precisa iniciar a linha ou vir depois de espaço | `http://x#frag` e `src/lib#2` nunca disparam. |
| Não pode ter espaço depois do `#` | Um heading (`# Título`) não é tag. |
| Caracteres aceitos: letras, dígitos, `-`, `_`, `/` | `#projeto/pandora` é uma tag aninhada só. |
| Precisa ter ao menos uma letra | `#123` é número no texto, não rótulo. |
| **Código é removido antes da busca** | Um `#comentário` dentro de bloco cercado ou inline não é tag. Os trechos são substituídos por espaços do mesmo tamanho, então nada fora deles muda de posição. |

O `TagName.ToSlug` então normaliza para a identidade: minúsculas, acentos removidos, mantendo `/`, `-`
e `_` (ao contrário do `Slugger`, que achata tudo em hífen), com teto de 50 caracteres. `#Café` e
`#cafe` são a mesma tag; o **nome** exibido é como foi escrita primeiro.

O espelho no frontend é `lib/tags.ts`. Duas manhas que vale conhecer ali: o regex teve que ser montado
com strings **aspeadas** (a crase escapada é escape inválido sob a flag `u`), e a cerca é escrita duas
vezes — fechada e correndo até o fim do texto — porque `$` ali significaria fim de *linha*, já que a
flag `m` é necessária para a regra de início de linha.

## 3. Sincronização e varredura de órfãs

O `PageTagSynchronizer` espelha o `PageLinkSynchronizer` — parseia o corpo, resolve as tags, faz
**diff** das arestas — com duas adições que o grafo de links não precisa:

- Ele **cria** as tags que o texto acabou de inventar; nenhuma tela de CRUD poderia tê-las criado.
- Ele **varre** as tags cuja última page acabou de largá-las, *a menos que tenham cor*. A cor é a
  única coisa que o texto não recupera, então é a única coisa que faz valer a pena manter viva uma
  linha vazia.

A varredura desconta as arestas removidas **na mesma transação** (o repositório ainda as enxerga)
passando o id da page que está sendo salva.

Deletar uma page roda o `ClearAsync`, que derruba todas as arestas dela e varre o que isso orfanou.

Duas notas de implementação com dentes:

- O EF precisou conhecer a FK `page_tag → tag` (`HasOne<Tag>().WithMany().HasForeignKey(…)`) mesmo sem
  propriedade de navegação. A varredura apaga a tag órfã e as arestas dela na mesma transação, e sem a
  relação declarada o EF mandava o `DELETE` da tag primeiro e batia em `fk_nte006_tag`. Foi um teste de
  integração que pegou.
- Re-salvar o mesmo texto não toca em nada, e nenhuma linha é apagada e reinserida dentro de uma
  transação — o `uq_nte006_page_tag` rejeitaria.

## 4. Cor

`PUT /notes/tags/{id}/color` é a única escrita. Só hex é aceito — `#rgb`, `#rrggbb`, `#rrggbbaa` —,
validado no handler porque o valor vai inline no `style` do chip. Cor inválida responde **422**, que é
como o projeto mapeia erro de validação.

## 5. Filtragem

O filtro é um query param `tagIds` em quatro rotas, e uma regra governa todas: **várias tags
intersectam (E)**. Duas tags selecionadas mostram as pages que carregam *as duas*. É a semântica de
"ir estreitando" que se espera de um filtro, e uma regra só em todo lugar é mais previsível que OU no
grafo e E na busca.

`TagFilter.MatchingPageIdsAsync` é a implementação única. Ela devolve `null` quando nenhuma tag foi
pedida — os chamadores leem isso como *sem filtro*, não como *nada casa* — e o resultado não é escopado
por dono, porque os chamadores o intersectam com as pages que já leram para o usuário.

| Superfície | Comportamento sob filtro |
|---|---|
| **Sidebar** (`GET /notes/pages`) | Vira **lista plana**, não árvore, e o drag-and-drop é desligado. Ver [Pages e hierarquia §7](pages-and-hierarchy.md#7-no-frontend). |
| **Busca** (`GET /notes/pages/search`) | Com `q` vazio, listar as pages daquela tag *é* a navegação. Lê `ResultLimit × 10` e corta depois — cortar antes deixaria de fora uma page que casa com os dois critérios. |
| **Grafo** (`/graph`, `/{id}/graph`) | Os nós são cortados **antes** do passeio pela vizinhança, então a profundidade é contada sobre o que sobrou. |
| **Page** | O `PageDto` carrega as tags da própria page, desenhadas como chips; clicar numa filtra a sidebar por ela. |

Os filtros vivem em estado de componente e **não entram na URL** — não existe link compartilhável de
"as pages com #x". Ver [Status de implementação](implementation-status.md).

## 6. Fora de escopo

- **Rename global** (§1).
- **Hierarquia real de tags aninhadas** — `#projeto/pandora` é aceito como texto da tag; não há rollup
  de `#projeto` incluindo as filhas.
- **Criar ou deletar tag por API.** O `TagsController` não tem POST nem DELETE de propósito: o texto as
  cria, a varredura as remove.
