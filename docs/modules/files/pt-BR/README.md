# Módulo Files

> Um catálogo de tudo o que está nos discos do usuário, dentro do monolito modular Pandora.
> 🇺🇸 [English version](../README.md).
>
> **Status: plano.** Nada foi construído. O Files vem depois das fases D1 (casco) e D2 (credenciais
> de dispositivo) do [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md) — ver
> [product-plan.md](product-plan.md).

O módulo **Files** indexa as pastas que o usuário escolhe nos seus discos — filmes, fotos, material
da faculdade, livros, manuais — para que ele navegue e busque o que tem de qualquer dispositivo, sem
o disco precisar estar acessível. Um agente desktop no PC que tem o disco percorre as pastas e relata
o que vê; o backend mantém o catálogo e percebe arquivos novos, alterados, sumidos e **movidos**. O
disco continua sendo a fonte de verdade: o agente nunca escreve nele, e o backend nunca guarda os
bytes.

---

## Como esta documentação está organizada

Como o [Assistant](../../assistant/pt-BR/README.md), o Files ainda não tem implementação, então não
tem o conjunto completo de tópicos `en/` + `pt-BR/`. O que existe hoje:

| Documento | Idioma | O que cobre |
|---|---|---|
| [Plano de Produto](product-plan.md) / [en](../en/product-plan.md) | en + pt-BR | Escopo, princípios, protocolo de scan, rascunho do modelo de dados, API, fases F1–F5 |
| [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md) / [en](../../../architecture/en/desktop-client.md) | en + pt-BR | O app desktop onde o agente roda: casco, ponte, módulos de desktop, credenciais de dispositivo (transversal) |

Quando a F1 estiver construída, o módulo passa para a estrutura usual por tópico (`overview.md`,
`architecture.md`, `data-model.md`, `scans.md`, `api-reference.md`, `implementation-status.md`), e o
`product-plan.md` fica só com o que falta.

---

## Fatos rápidos

- **Backend:** não iniciado. Alvo `Pottmayer.Pandora.Modules.Files.*`, schema `files`, tabelas
  `filXXX_`.
- **Agente:** `Pottmayer.Pandora.Desktop.Files`, um módulo do Pandora Desktop, autenticado com uma
  chave de dispositivo com escopo `files.agent`.
- **Frontend:** `client-web/src/modules/files`; a configuração é editável de qualquer lugar, enquanto
  o seletor de pasta nativo e a árvore de pastas ao vivo só aparecem dentro do app desktop, no próprio
  dispositivo.
- **Customizável em todos os níveis:** quantos dispositivos e raízes quiser, uma árvore de seleção de
  pastas por raiz, e filtros (extensão, glob, prefixo, sufixo, contém, regex) com escopo no usuário,
  num dispositivo, numa raiz ou numa pasta. A configuração vive no servidor.
- **Desligado por padrão**, duas vezes: interruptor da conta no servidor, interruptor do dispositivo
  no PC.
- **Depende de:** [Pandora Desktop](../../../architecture/pt-BR/desktop-client.md) (D1, D2),
  [Identity](../../identity/pt-BR/README.md) (dispositivos).
