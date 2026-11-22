// The emoji `:` offers while typing (`:fogo`, `:check`). A short list with English and Portuguese
// words, not a full library: the operating system's own panel (Win+. or Ctrl+Cmd+Space) has the rest.

export interface Emoji {
  char: string
  /** Words it is found by; the first is its name in the menu. */
  words: string[]
}

const list: [string, string][] = [
  ['😀', 'smile sorriso feliz happy'], ['😂', 'joy rindo risada laugh'], ['🙂', 'slight sorriso leve'], ['😉', 'wink piscada'],
  ['😍', 'love amor apaixonado heart-eyes'], ['😎', 'cool oculos legal'], ['🤔', 'thinking pensando hmm'], ['😅', 'sweat ufa nervoso'],
  ['😢', 'cry triste chorando sad'], ['😡', 'angry bravo raiva'], ['😴', 'sleep sono dormindo'], ['🤯', 'mind-blown explodiu chocado'],
  ['🥳', 'party festa celebrar'], ['😬', 'grimace eita tenso'], ['🙄', 'eyeroll revirar'], ['😇', 'angel anjo inocente'],
  ['👍', 'thumbsup joinha ok like'], ['👎', 'thumbsdown nao dislike'], ['👏', 'clap palmas aplausos'], ['🙏', 'pray obrigado please por-favor'],
  ['🙌', 'raise viva hooray'], ['👋', 'wave tchau oi hello'], ['💪', 'muscle forca strong'], ['🤝', 'handshake acordo deal'],
  ['👀', 'eyes olhos olhando'], ['🧠', 'brain cerebro ideia'], ['❤️', 'heart coracao amor love'], ['💔', 'broken coracao-partido'],
  ['🔥', 'fire fogo hot quente'], ['✨', 'sparkles brilho novo new'], ['⭐', 'star estrela favorito'], ['🌟', 'glowing estrela-brilhante'],
  ['⚡', 'zap raio rapido energia'], ['💡', 'bulb ideia lampada idea'], ['🎉', 'tada festa parabens celebrate'], ['🎯', 'target alvo meta goal'],
  ['🚀', 'rocket foguete lancamento launch'], ['🏆', 'trophy trofeu vitoria win'], ['🥇', 'medal medalha primeiro'], ['🎁', 'gift presente'],
  ['✅', 'check feito ok done sim'], ['☑️', 'ballot check-box marcado'], ['✔️', 'checkmark certo'], ['❌', 'x erro nao cross wrong'],
  ['⚠️', 'warning aviso atencao cuidado'], ['⛔', 'no-entry proibido bloqueado'], ['🚫', 'prohibited proibido nao'], ['❓', 'question pergunta duvida'],
  ['❗', 'exclamation importante'], ['💯', 'hundred cem perfeito'], ['🆕', 'new novo'], ['🔔', 'bell sino notificacao'],
  ['📌', 'pin fixar alfinete'], ['📍', 'location local lugar mapa'], ['🔗', 'link corrente'], ['🔒', 'lock cadeado seguro privado'],
  ['🔑', 'key chave senha'], ['🛠️', 'tools ferramentas'], ['🔧', 'wrench chave-inglesa ajuste'], ['⚙️', 'gear engrenagem config settings'],
  ['🐛', 'bug inseto erro'], ['🧪', 'test teste experimento'], ['📦', 'package pacote caixa'], ['🗑️', 'trash lixo excluir delete'],
  ['📝', 'memo nota anotacao note'], ['📄', 'page pagina documento doc'], ['📁', 'folder pasta'], ['📚', 'books livros estudo'],
  ['📖', 'book livro leitura'], ['📊', 'chart grafico dados'], ['📈', 'up subindo crescimento growth'], ['📉', 'down caindo queda'],
  ['📅', 'calendar calendario data'], ['⏰', 'alarm alarme despertador'], ['⏳', 'hourglass ampulheta espera'], ['🕒', 'clock relogio hora'],
  ['💰', 'money dinheiro grana'], ['💳', 'card cartao credito'], ['🏦', 'bank banco'], ['🧾', 'receipt recibo nota-fiscal'],
  ['📧', 'email e-mail'], ['💬', 'chat conversa mensagem'], ['📞', 'phone telefone ligacao'], ['📣', 'megaphone anuncio aviso'],
  ['💻', 'laptop computador notebook'], ['🖥️', 'desktop monitor'], ['📱', 'mobile celular'], ['☁️', 'cloud nuvem'],
  ['🐳', 'docker baleia whale'], ['🐍', 'python cobra snake'], ['☕', 'coffee cafe java'], ['🦀', 'rust caranguejo crab'],
  ['🏠', 'home casa'], ['🏢', 'office escritorio empresa'], ['✈️', 'plane aviao viagem travel'], ['🚗', 'car carro'],
  ['🍕', 'pizza comida'], ['🍔', 'burger hamburguer'], ['🍺', 'beer cerveja'], ['🎂', 'cake bolo aniversario birthday'],
  ['⚽', 'soccer futebol bola'], ['🎮', 'game jogo videogame'], ['🎵', 'music musica'], ['🎬', 'movie filme cinema'],
  ['🌱', 'seedling broto planta comeco'], ['🌳', 'tree arvore'], ['🌞', 'sun sol'], ['🌙', 'moon lua noite'],
  ['🌧️', 'rain chuva'], ['❄️', 'snow neve frio'], ['🐶', 'dog cachorro'], ['🐱', 'cat gato'],
  ['🟢', 'green verde circulo'], ['🟡', 'yellow amarelo circulo'], ['🔴', 'red vermelho circulo'], ['🔵', 'blue azul circulo'],
  ['⬆️', 'up cima seta arrow'], ['⬇️', 'down baixo seta arrow'], ['➡️', 'right direita seta arrow'], ['⬅️', 'left esquerda seta arrow'],
]

export const EMOJIS: Emoji[] = list.map(([char, words]) => ({ char, words: words.split(' ') }))

/** The emoji whose words start with what was typed after `:`; nothing until a letter is typed. */
export function filterEmojis(query: string, limit = 8): Emoji[] {
  const term = query.trim().toLowerCase()
  if (!term) return []
  return EMOJIS.filter((emoji) => emoji.words.some((word) => word.startsWith(term))).slice(0, limit)
}
