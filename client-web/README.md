# client-web

Pandora's browser app: React 19, TypeScript, Vite, Ant Design 6, Tailwind, TanStack Query, i18next.

- [Frontend architecture](../docs/architecture/en/frontend.md) ([pt-BR](../docs/architecture/pt-BR/frontend.md)) — folders, data, routing, language, tests.
- [Pandora's look](DESIGN.md) — themes, tokens and the screen patterns.
- [Project and code standards](../docs/architecture/en/coding-standards.md) — rules shared with the backend.

## Running

```bash
npm install
```

```bash
npm run dev
```

The dev server runs on http://localhost:5173 and talks to the backend at `https://localhost:61182`
(override with `VITE_API_URL`).

## Checking

```bash
npm test
```

```bash
npx tsc -b --noEmit
```

```bash
npm run lint
```

```bash
npm run build
```
