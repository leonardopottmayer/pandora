/** A calendar's color as a faint fill: the text on it stays the theme's, readable on any color. */
export const tint = (color: string, alpha = '29') => `${color.slice(0, 7)}${alpha}`
