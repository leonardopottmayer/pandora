// Mermaid and KaTeX are large: loaded the first time a page shows a diagram or a formula.

let mermaidModule: Promise<typeof import('mermaid').default> | null = null
export const loadMermaid = () => (mermaidModule ??= import('mermaid').then((module) => module.default))

let katexModule: Promise<typeof import('katex').default> | null = null
export const loadKatex = () =>
  (katexModule ??= Promise.all([import('katex'), import('katex/dist/katex.min.css')]).then(([module]) => module.default))
