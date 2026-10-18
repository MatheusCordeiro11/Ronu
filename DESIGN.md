---
name: Ronu
description: Dieta calculada a partir do gasto calórico real do seu treino de artes marciais
colors:
  fundo: "#0F0E0D"
  superficie: "#151311"
  superficie-2: "#1A1816"
  superficie-3: "#1F1C19"
  borda: "#2C2925"
  linha: "#3A3631"
  borda-campo: "#65615A"
  texto: "#F0E9DD"
  texto-2: "#A39C91"
  texto-3: "#6E675E"
  acento: "#E5533B"
  sobre-acento: "#0F0E0D"
  erro-campo: "#B5462F"
  washi-fundo: "#F2ECE0"
  washi-superficie: "#EAE3D5"
  washi-superficie-2: "#E4DCCC"
  washi-superficie-3: "#DFD6C5"
  washi-borda: "#CFC4B0"
  washi-linha: "#BFB39D"
  washi-borda-campo: "#898174"
  washi-texto: "#1A1714"
  washi-texto-2: "#6B6254"
  washi-texto-3: "#857A68"
  washi-acento: "#C23B22"
  washi-acento-texto: "#A53520"
  washi-sobre-acento: "#F2ECE0"
typography:
  display:
    fontFamily: "'Shippori Mincho B1', Georgia, serif"
    fontSize: "clamp(2.5rem, 1.6rem + 3.6vw, 4rem)"
    fontWeight: 800
    lineHeight: 1.04
  title:
    fontFamily: "'Shippori Mincho B1', Georgia, serif"
    fontSize: "clamp(1.875rem, 1.35rem + 1.9vw, 2.75rem)"
    fontWeight: 800
    lineHeight: 1.1
  body:
    fontFamily: "'IBM Plex Sans', -apple-system, sans-serif"
    fontSize: "0.9375rem"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "'IBM Plex Sans', -apple-system, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 500
    letterSpacing: "0.2em"
  mono:
    fontFamily: "'IBM Plex Mono', monospace"
    fontSize: "0.75rem"
    fontWeight: 500
    letterSpacing: "0.06em"
rounded:
  campo: "3px"
  opcao: "4px"
  card: "6px"
  pilula: "999px"
spacing:
  e-2: "0.5rem"
  e-3: "0.75rem"
  e-4: "1rem"
  e-6: "1.5rem"
  e-7: "2rem"
  e-9: "3rem"
  e-11: "6rem"
components:
  button-primary:
    backgroundColor: "{colors.acento}"
    textColor: "{colors.sobre-acento}"
    rounded: "{rounded.campo}"
    padding: "12px 22px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.texto}"
    rounded: "{rounded.campo}"
    padding: "12px 22px"
---

# Design System: Ronu

## Overview

**Creative North Star: "Dojo Noturno"**

Um dojo depois do treino: luz baixa, tatame escuro, um quimono pendurado e o carimbo vermelho do 浪人 (rōnin, o lutador sem mestre que dá nome ao Ronu) na ficha de cada aluno. O Ronu é sério e preciso como uma pesagem oficial, mas tem o calor de papel e tinta: serifado japonês nos títulos e números, sans técnico no corpo, um vermelho de carimbo como único acento.

O sistema tem **dois modos**, com a mesma estrutura e os mesmos componentes:

- **Dojo Noturno** (escuro, a identidade): fundo quase preto quente, texto cor de papel cru, acento vermelho-carimbo.
- **Washi** (claro): o papel japonês. Fundo creme, texto tinta, o mesmo vermelho um tom mais fechado.

A pessoa troca pelo sol/lua no topo ou pela opção Aparência no Perfil (Escuro, Claro ou Igual ao aparelho). Na primeira visita vale o `prefers-color-scheme` do aparelho; depois, a escolha fica no `localStorage` (`ronu-tema`).

**Key Characteristics:**
- Uma lista única de variáveis de cor (`frontend/css/base/tokens.css`) com duas versões. Nenhuma tela usa cor escrita direto.
- Um acento só, vermelho-carimbo: CTA, item escolhido, foco, dia de treino. Nunca preenche áreas grandes.
- Profundidade por tom de superfície e borda de 1px. Sem sombra.
- Números de métrica sempre no serifado e em `tabular-nums`.
- Movimento com propósito: os gráficos se desenham; só o enfeite se move sem parar.

## Colors

Os pares exatos vieram das telas aprovadas em `docs/redesign/telas` (cada tela comparada com a sua versão `…Washi`).

| Variável | Escuro | Washi | Papel |
|---|---|---|---|
| `--fundo` | `#0F0E0D` | `#F2ECE0` | página, campos sobre card |
| `--superficie` | `#151311` | `#EAE3D5` | cards, campos |
| `--superficie-2` | `#1A1816` | `#E4DCCC` | card flutuante, catálogo |
| `--superficie-3` | `#1F1C19` | `#DFD6C5` | item ativo (nav, opção marcada, avatar) |
| `--superficie-hover` | `#24201C` | `#DCD2C0` | hover de card |
| `--borda` | `#2C2925` | `#CFC4B0` | borda de card e divisória |
| `--linha` | `#3A3631` | `#BFB39D` | chips, botão fantasma, etapa futura |
| `--borda-hover` / `--borda-forte` | `#4A443D` / `#5C564D` | `#A89C86` / `#9A8E78` | hover, barra negativa |
| `--borda-campo` | `#65615A` | `#898174` | contorno de campo (3:1) |
| `--divisor` / `--grade` | `#221F1C` / `#211E1A` | `#D6CCB9` / `#E2D9C8` | linhas internas, quadriculado |
| `--texto` | `#F0E9DD` | `#1A1714` | texto principal |
| `--texto-2` | `#A39C91` | `#6B6254` | texto secundário |
| `--texto-2-ativo` | `#A39C91` | `#645B4E` | secundário sobre `--superficie-3` |
| `--texto-3` | `#6E675E` | `#857A68` | **só** placeholder e enfeite |
| `--acento` | `#E5533B` | `#C23B22` | destaque |
| `--acento-texto` | `#E5533B` | `#A53520` | acento como texto pequeno |
| `--sobre-acento` | `#0F0E0D` | `#F2ECE0` | texto em cima do acento |
| `--erro-borda` / `--erro-fundo` / `--erro-campo` | `#7A2E22` / `#2A1512` / `#B5462F` | `#D9A79B` / `#F7E1DB` / `#B23A24` | erro |
| `--sucesso-borda` / `--sucesso-fundo` | `#3F5A3A` / `#151F14` | `#9DB592` / `#E3EDDC` | sucesso |

O quimono tem variáveis próprias (`--quimono*`): branco com faixa preta no escuro, preto com faixa clara no Washi. As faixas do onboarding (azul `#2F5D8A`, roxa `#6A3E7C`, marrom `#6B4A2E`) são iguais nos dois modos.

### Named Rules
**A Regra do Carimbo.** O vermelho marca, não pinta: botão principal, a opção escolhida, o dia de treino, o foco. Se uma área grande ficou vermelha, é carimbo demais.

**A Regra do Contraste Medido.** Texto que precisa ser lido usa `--texto` ou `--texto-2` (≥ 4,5:1 nos dois modos). `--texto-3` fica para placeholder e enfeite. Acento como texto pequeno usa `--acento-texto`.

## Typography

**Títulos e números:** Shippori Mincho B1 (600/800), serifado japonês de traço firme.
**Corpo:** IBM Plex Sans (400/500/600), técnico e legível.
**Etiquetas da landing:** IBM Plex Mono (500), maiúsculas espaçadas, só na rolagem guiada.

### Hierarchy
- **Display** (800, 40→64px): título do hero da landing.
- **Título de tela** (800, 30→44px): `h1` de cada tela.
- **Seção** (800, 24px) e **título de card** (800, 22px).
- **Número grande** (`.numero`, 800): meta do dia, peso de tendência, totais; unidade em sans, menor e em `--texto-2`.
- **Corpo** (400, 15–17px, line-height 1.6).
- **Rótulo** (`.rotulo`, 500, 12px, maiúsculas, `letter-spacing: 0.2em`, `--texto-2` ou acento).

### Named Rules
**A Regra do Número Estável.** Todo número de métrica usa `tabular-nums` (`.num`, `.numero`, tabelas).

## Layout

Contêiner único de 1320px com margem de 40px (16px abaixo de 720px). Cada tela logada tem: cabeçalho (logo, nav, sol/lua, avatar), faixa de título sobre a grade quadriculada e o conteúdo em duas colunas flexíveis (principal ~640px+, lateral ~340px+), que viram uma coluna no celular. No celular, a nav do topo dá lugar a 5 abas fixas embaixo, com ícone (Dieta, Progresso, Treino, Alimentação, Perfil).

Telas de entrada (login, cadastro, senha, estado, onboarding) dividem a tela: à esquerda, um painel sobre a grade com logo, sol/lua, quimono e carimbo; à direita, o formulário.

## Elevation & Depth

Plano. Profundidade = tom de superfície (`--fundo` → `--superficie` → `--superficie-3`) mais borda de 1px. Hover de card: zoom leve (`scale(1.012)`) e borda mais clara, só em ponteiro fino.

### Named Rules
**A Regra Sem Sombra.** Nenhum `box-shadow`, glow ou blur decorativo.

## Shapes

Cantos de 3px em botões e campos, 4px em opções e carimbo, 6px em cards. Pílula (999px) só em chips e controles pequenos.

## Motion

- **Enfeite, contínuo:** grade deslizando (18s), quimono balançando (9s), carimbo batendo (7s), cards flutuando.
- **Dados, sob demanda:** gráficos se desenham ao carregar, ao voltar à tela pela rolagem e no hover (`[data-anima-grupo]`, `js/base/movimento.js`).
- **Landing:** cada seção gruda na tela e a rolagem controla a cena por uma variável `--p` (0 a 1), via `requestAnimationFrame` com suavização. Sem rolagem automática.
- **Troca de tema:** cruzamento de 280ms por View Transition.
- `prefers-reduced-motion: reduce` desliga tudo; o conteúdo aparece no estado final.

## Components

Em `frontend/css/componentes.css`; o estilo próprio de cada tela fica em `frontend/css/telas/`:

- **Logo:** quadrado no acento com 浪 + "RONU" no serifado.
- **Botões:** primário (acento, texto `--sobre-acento`, hover por brilho), fantasma (borda `--linha`), texto (sublinhado). Carregando = `aria-busy` com linha correndo; desabilitado = `disabled`.
- **Campos:** `.campo` + `.entrada`, rótulo acima, borda `--borda-campo`, foco no acento, erro com `aria-invalid` e `--erro-campo`.
- **Escolhas:** `.segmentado` (normal ou `-acento`), `.opcao` (card de rádio), `.chip` (marcado, gosto, evitar tracejado, sugestão), `.dias` (7 botões).
- **Cards:** `.cartao` com `.rotulo` no topo e link de ação no acento.
- **Avisos:** `.aviso-erro`, `.aviso-sucesso`; `.nota` com traço curto no acento.
- **Marca:** `.carimbo` 浪人, `.quimono` (SVG provisório), `.grade`.
- **Progresso:** `.etapas`, `.trilho`/`.preenche`, anel, `.carregando`.

## Do's and Don'ts

### Do:
- **Do** usar só as variáveis de `tokens.css`; conferir cada tela nos dois modos.
- **Do** dar a todo controle um botão ou link de verdade, rótulo visível ou `aria-label`, e foco visível.
- **Do** usar o botão oficial do Google (`renderButton`), redesenhado no tema certo quando o modo troca.

### Don't:
- **Don't** escrever cor direto em tela nenhuma.
- **Don't** usar `--texto-3` em texto que precisa ser lido.
- **Don't** pôr sombra, gradiente decorativo ou cantos acima de 6px em card.
- **Don't** animar gráfico em laço infinito.
- **Don't** inventar dados, depoimentos ou modalidades: tudo que é número ou catálogo vem da API.
