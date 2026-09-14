---
name: Ronu
description: Dieta calculada a partir do gasto calórico real do seu treino de artes marciais
colors:
  tatame-noturno: "#121820"
  painel-academia: "#1B232D"
  corda-crua: "#EDE7DA"
  poeira-tatame: "#8F98A3"
  ouro-faixa: "#C9A227"
  linha-demarcacao: "#2A333F"
  ferrugem-ferro: "#A8462F"
typography:
  display:
    fontFamily: "'Space Grotesk', Georgia, serif"
    fontSize: "clamp(2.5rem, 5vw, 4.5rem)"
    fontWeight: 600
    lineHeight: 1.1
    letterSpacing: "-0.01em"
  body:
    fontFamily: "'Inter', -apple-system, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "'Inter', -apple-system, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 500
    lineHeight: 1.4
rounded:
  default: "4px"
spacing:
  sp-1: "0.5rem"
  sp-2: "0.75rem"
  sp-3: "1rem"
  sp-4: "1.5rem"
  sp-5: "2rem"
  sp-6: "3rem"
  sp-7: "4rem"
  sp-8: "6rem"
  sp-9: "8rem"
components:
  button-primary:
    backgroundColor: "{colors.ouro-faixa}"
    textColor: "{colors.tatame-noturno}"
    rounded: "{rounded.default}"
    padding: "16px 32px"
  button-primary-hover:
    backgroundColor: "#b8931f"
    textColor: "{colors.tatame-noturno}"
  button-secondary:
    backgroundColor: "transparent"
    textColor: "{colors.corda-crua}"
    rounded: "{rounded.default}"
    padding: "16px 32px"
---

# Design System: Ronu

## Overview

**Creative North Star: "A Súmula do Tatame"**

A súmula é a ficha oficial de uma luta: preenchida à mão, sem espaço para floreio, cada número nela é exato porque decide um resultado. É essa mistura que define o Ronu — a seriedade do tatame, a precisão de uma pesagem oficial e o hábito de registrar treino como um logbook de atleta, tudo em um único documento visual. Nada aqui existe para impressionar; existe para estar certo.

O sistema é escuro, denso e de baixo contraste decorativo: um fundo quase preto, texto num branco quente (cor de corda crua, não um branco de tela), e um único acento dourado — a cor de faixa — usado como um selo, não como papel de parede. Bordas finas de 1px substituem sombra; a profundidade vem do contraste entre painel e fundo, nunca de um blur suave. Cantos ficam quase retos (4px): o suficiente para não cortar o dedo, não o bastante para virar decoração.

**Key Characteristics:**
- Escuro, denso, sem gradiente decorativo — a cor sobra para o dourado, não para o fundo.
- Um acento só (dourado), usado como carimbo raro: CTA, eyebrow, borda de destaque.
- Tipografia forte no display (Space Grotesk), neutra e legível no corpo (Inter).
- Zero sombra. Profundidade = contraste de superfície + linha de 1px.
- Números (peso, frequência, calorias) sempre em `tabular-nums` — a súmula não deixa o dado "dançar".

## Colors

Paleta de baixa saturação e alto contraste tonal: o fundo e o painel são variações do mesmo azul-ardósia quase preto; o texto é um branco quente, não neutro; o dourado é a única cor viva do sistema e aparece em doses homeopáticas.

### Primary
- **Ouro de Faixa** (`#C9A227`): o único acento do sistema — CTA primário, eyebrow, borda de destaque em citações, foco de campo de formulário. Sobre ele, o texto sempre usa Tatame Noturno (`#121820`), nunca branco.

### Neutral
- **Tatame Noturno** (`#121820`): fundo base de toda a interface.
- **Painel de Academia** (`#1B232D`): superfície elevada — seções alternadas, cards, blocos de destaque. Não há sombra entre ele e o fundo; a separação é só a mudança de tom.
- **Corda Crua** (`#EDE7DA`): cor do texto principal — um branco quente, nunca `#FFFFFF` puro.
- **Poeira de Tatame** (`#8F98A3`): texto secundário/muted — legendas, texto de apoio, placeholders.
- **Linha de Demarcação** (`#2A333F`): toda borda, divisor e linha de grade do sistema.

### Danger
- **Ferrugem de Ferro** (`#A8462F`): erro de formulário e mensagens de falha. Nunca usado decorativamente.

### Named Rules
**A Regra do Ouro Raro.** O dourado nunca preenche uma área grande — ele pontua (texto de eyebrow, borda de CTA, foco de input), nunca decora. Se uma tela tem mais de um elemento dourado grande por vez, tem dourado demais.

## Typography

**Display Font:** Space Grotesk (fallback: Georgia, serif)
**Body Font:** Inter (fallback: -apple-system, sans-serif)

**Character:** Space Grotesk carrega o peso institucional — títulos e números que precisam parecer definitivos, quase gravados. Inter carrega a leitura corrida — neutro, sem personalidade própria, para não competir com o display.

### Hierarchy
- **Display** (600, `clamp(2.5rem, 5vw, 4.5rem)`, line-height 1.1): título de hero e headings de seção grandes (`h1`–`h2`).
- **Title** (600, 1.375–1.75rem, line-height 1.1): subtítulos de seção, título de card.
- **Body** (400, 1rem, line-height 1.6): parágrafo corrido, sempre em Poeira de Tatame a menos que marcado `.text-strong`.
- **Label** (500, 0.875rem): rótulo de campo, eyebrow, texto de botão, nav.

### Named Rules
**A Regra do Número Estável.** Todo número que representa uma métrica do usuário — peso, frequência semanal, calorias — usa `font-variant-numeric: tabular-nums` (classe `.num`). Uma súmula não deixa o dado "dançar" quando o dígito muda.

## Layout

Grid de container único (`max-width: 1200px`, padding lateral de `--sp-4`/24px, reduzindo para `--sp-3`/16px abaixo de 720px). Seções empilham verticalmente com respiro generoso (`--sp-8`/96px de padding vertical, reduzindo para `--sp-6`/48px em mobile) e são separadas por uma única linha de 1px (`border-top`/`border-bottom`), nunca por um card flutuante.

Layouts de duas colunas (hero, seção de problema) usam grid assimétrico (ex.: `0.9fr` / `1.1fr`) e colapsam para uma coluna abaixo de 960px, com a imagem/slot visual sempre subindo para o topo da pilha em mobile (`order: -1`).

Escala de espaçamento em base 8px, de `--sp-1` (8px) a `--sp-9` (128px) — usada tanto para gaps de grid quanto para padding interno de componentes.

## Elevation & Depth

Sistema inteiramente plano. Não existe `box-shadow` em nenhum componente. Profundidade é comunicada só por contraste tonal entre `Tatame Noturno` (fundo) e `Painel de Academia` (superfície elevada), reforçado por uma linha de 1px em `Linha de Demarcação` onde as duas se encontram.

### Named Rules
**A Regra Sem Sombra.** Nenhum componente recebe `box-shadow`, blur ou glow difuso. Se um elemento precisa se destacar, ele muda de tom de fundo ou ganha uma borda — nunca uma sombra.

## Shapes

Cantos quase retos: um único raio de 4px em todo o sistema (botões, inputs, cards, o slot de imagem do hero) — o suficiente para suavizar a aresta, não o bastante para parecer amigável ou orgânico. Bordas são sempre 1px, sólidas, na cor `Linha de Demarcação`; não há bordas grossas nem duplas.

O slot de imagem do hero usa uma grade fina sobreposta (linhas de 1px formando uma malha 3×3) como textura de fundo enquanto a imagem real não entra — um motivo gráfico de "grade calculada" coerente com o tom analítico do produto, reaproveitável em outros placeholders visuais.

## Components

### Buttons
- **Shape:** raio de 4px, sem borda visível no primário.
- **Primary:** fundo Ouro de Faixa, texto Tatame Noturno, padding `16px 32px` (`--sp-3` `--sp-5`), peso 600, sem sombra.
- **Hover:** fundo escurece para `#b8931f` (mesma cor, ~7% mais escura) — nunca escala nem ganha sombra.
- **Secondary/Ghost:** fundo transparente, borda 1px em `Linha de Demarcação`, texto Corda Crua; hover clareia a borda para Poeira de Tatame. Usado para ações secundárias ("Já tenho uma conta").

### Inputs / Fields
- **Style:** fundo Tatame Noturno, borda 1px em `Linha de Demarcação`, raio 4px, padding `--sp-3` (16px), rótulo acima em Label (0.875rem, 500).
- **Focus:** a borda muda para Ouro de Faixa — nenhum glow, nenhuma sombra de foco.
- **Error:** borda em Ferrugem de Ferro, mensagem de erro abaixo do campo no mesmo tom.
- **Mensagem de formulário (banner):** bloco com borda 1px e fundo em tom translúcido da cor de status (`rgba` a 8–10% de opacidade sobre Ouro de Faixa para sucesso, sobre Ferrugem de Ferro para erro) — nunca um fundo sólido saturado.

### Navigation
- **Style:** wordmark em Space Grotesk 700 ("ronu" + ponto final em Ouro de Faixa), links de nav em Label sobre Poeira de Tatame, clareando para Corda Crua no hover. Sem sublinhado, sem pill de fundo ativo.

### Signature: Eyebrow
Rótulo curto acima de um título (`.eyebrow`): um traço horizontal de 14px em Ouro de Faixa antes do texto, texto em Label weight 500 na cor do acento. É o único lugar onde o dourado aparece em texto corrido, reforçando a Regra do Ouro Raro.

## Do's and Don'ts

### Do:
- **Do** manter o dourado (`#C9A227`) restrito a pontuação: CTA primário, eyebrow, foco de input, borda de destaque em citação.
- **Do** usar `tabular-nums` em qualquer número que represente peso, frequência ou calorias.
- **Do** separar seções com uma linha de 1px em `Linha de Demarcação`, nunca com um card flutuante ou sombra.
- **Do** manter o raio em 4px em todos os componentes — botão, input, card, slot de imagem.

### Don't:
- **Don't** usar gradiente roxo/azul-arroxeado ou qualquer gradiente decorativo fora do sutil overlay dourado já definido no slot de imagem do hero.
- **Don't** adicionar `box-shadow`, glow ou blur em qualquer componente — profundidade vem só de contraste de superfície.
- **Don't** arredondar cantos além de 4px — nada de cards ou botões "pill".
- **Don't** usar branco puro (`#FFFFFF`) para texto — o texto principal é sempre Corda Crua (`#EDE7DA`).
- **Don't** inventar prova social (depoimentos, fotos de atletas, números de usuários) — nenhum conteúdo real existe ainda; usar placeholders explícitos como o slot de imagem do hero.
