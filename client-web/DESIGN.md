# Pandora's look

Pandora stays open all day and mixes tables, notes and a calendar. The user picks how it looks, and the screens stay out of it.

The values live in [`src/app/theme.ts`](src/app/theme.ts), on top of antd's own algorithms.

## Three choices

| Choice | Values | Stored in |
|---|---|---|
| Mode | light, dark, system | `idt003.theme` |
| Theme | Default, Claude, Reddit | `idt003.palette` |
| Accent | the theme's own, or any color | `idt003.accent_color`, null for the theme's own |

They combine freely, so a new theme is one more block in `PALETTES`, in both modes, and nothing else. Claude's and Reddit's colors are inspired by their dark and light modes, and were not measured from them.

## The accent

The user picks one color and antd derives its hover, active and selected tones. Three things are worked out so that any color works:

- **Text on the accent** is white, or navy `#181b3d` where white falls under 3:1.
- **The accent as text**, in links and the selected menu item, moves toward white or black until it reaches 4.5:1 on every surface.
- **Green, red and yellow stay fixed.** They say success, error and warning, and don't follow the accent.

The ready-made ones are Ant Design's blue `#1677ff` and green `#2dcd88`, whose hover is `#2dbc88`.

`theme.test.ts` checks this on every combination of theme, mode and a range of accents, including white, near-black and yellow.

## Rules

- **Colors come from tokens.** No hex in a screen: `theme.useToken()` in code, `var(--ant-color-*)` in CSS. Data the user picked, like a calendar's color, is the exception.
- **On an accent background, use `onAccent()`,** not white.
- **One accent.** The primary is for the one main action of a view and for what is selected. Every other button is the default.
- **Color carries meaning.** Green, red and yellow are not decoration.
- **Change the theme before the screen.** If many screens need the same change, it belongs in `theme.ts`. Before overriding a component, check its tokens with `antd token <Component>`.

## Type and layout

- **Type:** the system font, at antd's sizes.
- **Numbers:** tabular, so amounts line up in columns.
- **Density:** antd's default size. Tables can go smaller when a screen asks for it, not globally.
- **Screen width:** settings and forms at `max-w-3xl`; lists and tables use the available width.
- **Settings screens** use `PageHeading`, `SettingsSection` and `SettingRow`.
- **Filters:** what is used most stays visible, the rest collapses. Filters should not push the data off the first screen.
