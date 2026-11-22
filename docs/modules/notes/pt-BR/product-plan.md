# Notes — Plano de produto (o que falta)

[← Voltar ao índice](README.md) · 🇺🇸 [English version](../en/product-plan.md)

> **Status:** a reforma em blocos (B1–B11) está construída para todos os módulos que existem. O que
> existe está no [Status de implementação](implementation-status.md) e nos documentos de tópico; este
> arquivo guarda só o que ainda não existe.

---

## Blocos que esperam outros módulos

| Bloco | Espera |
|---|---|
| Um item do **Radar** salvo como página | [Radar](../../radar/pt-BR/README.md) |
| Uma visão do **Databases** dentro de uma página | [Databases](../../databases/pt-BR/README.md) |
| Um board ou lista de itens do **Projects** dentro de uma página | [Projects](../../projects/pt-BR/README.md) |

Eles entram no grupo **Dados** do menu `/`, como os blocos do Agenda e do Finances, e seguem a mesma
regra: o bloco guarda só a configuração, lê os endpoints do próprio outro módulo, e uma página pública
mostra "Conteúdo privado".

## Depois (sem data)

- **Relatório de propriedades** entre páginas (o "page properties report" do Confluence); a visão de
  tabela da pasta cobre uma pasta hoje.
- **Edição inline** na tabela de propriedades da pasta.
- **Datas fixas** no bloco do Agenda; ele mostra janelas móveis de 7 ou 30 dias.
- **Filtros de tag na URL**, como parâmetro de busca no `NotesPage`.
- **Armazenamento em bucket e upload de vídeo:** um backend em bucket (cada linha de mídia já guarda o
  backend), downloads por faixa e uma miniatura de quadro do vídeo.
- **Ideias do grafo:** uma visão das pontes entre famílias, layouts salvos, comunidades automáticas.
  Ver [Grafo](graph-view.md).
