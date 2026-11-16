---
name: Ronu
description: Dieta semanal calculada pelo gasto calórico real do seu treino, de artes marciais a corrida, com 26 modalidades
colors:
  fundo: "#0F0E0D"
  superficie: "#151311"
  superficie-2: "#1A1816"
  superficie-3: "#1F1C19"
  borda: "#2C2925"
  borda-campo: "#65615A"
  texto: "#F0E9DD"
  texto-2: "#A39C91"
  texto-3: "#6E675E"
  acento: "#E5533B"
  acento-texto: "#E5533B"
  acento-suave: "#2A1512"
  sobre-acento: "#0F0E0D"
  verde: "#7BB878"
  erro: "#F07A62"
  erro-suave: "#2A1512"
  washi-fundo: "#FBF8F3"
  washi-superficie: "#FFFFFF"
  washi-superficie-2: "#F2ECE0"
  washi-superficie-3: "#DFD6C5"
  washi-borda: "#E8E0D2"
  washi-borda-campo: "#898174"
  washi-texto: "#1A1714"
  washi-texto-2: "#6B6254"
  washi-texto-3: "#A0957F"
  washi-acento: "#C23B22"
  washi-acento-texto: "#A53520"
  washi-acento-suave: "#FBE7E1"
  washi-sobre-acento: "#FFFFFF"
  washi-verde: "#2F7A4A"
  washi-erro: "#B23A24"
  washi-erro-suave: "#FBE4DE"
typography:
  display:
    fontFamily: "'Fustat', 'Segoe UI', system-ui, sans-serif"
    fontSize: "clamp(2.75rem, 1.8rem + 4vw, 4.5rem)"
    fontWeight: 800
    lineHeight: 1.02
    letterSpacing: "-0.045em"
  title:
    fontFamily: "'Fustat', 'Segoe UI', system-ui, sans-serif"
    fontSize: "clamp(2rem, 1.5rem + 1.6vw, 2.875rem)"
    fontWeight: 800
    lineHeight: 1.08
    letterSpacing: "-0.04em"
  body:
    fontFamily: "'Fustat', 'Segoe UI', system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "'Fustat', 'Segoe UI', system-ui, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 800
    letterSpacing: "0.08em"
  number:
    fontFamily: "'Fustat', 'Segoe UI', system-ui, sans-serif"
    fontSize: "clamp(2.5rem, 2rem + 2vw, 3.375rem)"
    fontWeight: 800
    letterSpacing: "-0.055em"
rounded:
  pequeno: "10px"
  campo: "14px"
  opcao: "18px"
  card: "24px"
  pilula: "999px"
spacing:
  e-2: "0.5rem"
  e-3: "0.75rem"
  e-4: "1rem"
  e-5: "1.25rem"
  e-6: "1.5rem"
  e-7: "2rem"
  e-9: "3rem"
  e-11: "6rem"
components:
  button-primary:
    backgroundColor: "{colors.acento}"
    textColor: "{colors.sobre-acento}"
    rounded: "{rounded.pilula}"
    padding: "12px 20px"
  button-outline:
    backgroundColor: "{colors.superficie}"
    textColor: "{colors.texto}"
    rounded: "{rounded.pilula}"
    padding: "12px 20px"
  card:
    backgroundColor: "{colors.superficie}"
    rounded: "{rounded.card}"
    padding: "22px"
  input:
    backgroundColor: "{colors.fundo}"
    textColor: "{colors.texto}"
    rounded: "{rounded.campo}"
    padding: "0 16px"
---

# Design System: Ronu

## Overview

**Creative North Star: "Dojo Noturno" e "Washi", em versão leve**

O Ronu é a mesa depois do treino: uma tigela de arroz com a faixa preta amarrada, o quimono dobrado ao lado e a balança no canto. O tom é de app moderno e caloroso, não de cartaz de artes marciais. A seriedade está na conta: cada número sai de uma fórmula que a pessoa pode abrir e conferir. A leveza está na forma: uma sans geométrica arredondada (Fustat), cards de cantos grandes, ícones 3D de massinha e movimento com mola.

O sistema tem **dois modos**, com a mesma estrutura e os mesmos componentes:

- **Washi** (claro): papel cru quente, cards brancos, texto tinta e o vermelho-carimbo.
- **Dojo Noturno** (escuro): o mesmo layout num preto quente, com texto cor de papel e o vermelho um tom mais vivo.

A pessoa troca pelo sol/lua do cabeçalho ou em Perfil → Aparência (Escuro, Claro ou Igual ao aparelho). O mecanismo é o de sempre: `js/base/tema.js` põe `data-tema="claro"` no `<html>` antes da primeira pintura, e a escolha fica no `localStorage` (`ronu-tema`). Na primeira visita vale o `prefers-color-scheme` do aparelho.

**Key Characteristics:**
- Toda cor sai de `frontend/css/base/tokens.css`, com duas versões. Nenhuma tela usa cor escrita direto.
- Um acento só, o vermelho-carimbo: botão principal, item escolhido, foco, dia de treino, palavra de destaque do título.
- Profundidade por superfície, borda de 1px **e** sombra suave e quente, sempre em token.
- Fustat em tudo: títulos pesados com espaçamento negativo, corpo regular.
- Ícones 3D de massinha (creme e vermelho) dão o calor. Ilustração em traço não é mais usada.
- Movimento com mola nos controles, revelação em cascata nos cards, laços lentos só em enfeite.

### Referência visual

Os protótipos aprovados em 2026-11-15 são a referência de cada tela. Eles são privados e só abrem na conta do dono:

| Tela | Protótipo |
|---|---|
| Landing | https://claude.ai/artifact/13ovpgKcuhLw5HLKWYYWLJ |
| Entrada e onboarding | https://claude.ai/artifact/GCJ8eKh4WRRS5Y3bvM94i4 |
| Painel da dieta | https://claude.ai/artifact/6f6dExJQRVsTqZHU3bDRRW |
| Progresso | https://claude.ai/artifact/Qk2mLCj4fJG7qjrmnodkzP |
| Treino | https://claude.ai/artifact/HH5hZFsUy8k4eaLZUorgsV |
| Alimentação | https://claude.ai/artifact/5dViTJ3KZVkse9CpV6GKJ4 |
| Lista de compras | https://claude.ai/artifact/7Zk1P8c3gYG85uKvkZU4za |
| Casa | https://claude.ai/artifact/YYVuouUy3cRxjVuudCsnek |
| Perfil | https://claude.ai/artifact/Jez5cJSEgjA1vQ9EuphpSi |
| Telas de senha | https://claude.ai/artifact/EBmFjaE3FCNcXQdBcMHWuw |

Onde este documento e um protótipo discordarem, vale este documento. Os ajustes de contraste abaixo corrigem os protótipos de propósito.

## Colors

A maior parte das variáveis mantém o nome da base atual e muda de valor; algumas saem ou trocam de nome, e entram papéis novos. A tabela "Migração dos tokens", no fim desta seção, cobre cada variável de `tokens.css`. Contraste medido pela fórmula do WCAG.

| Variável | Escuro | Washi | Papel |
|---|---|---|---|
| `--fundo` | `#0F0E0D` | `#FBF8F3` | página; fundo dos campos |
| `--superficie` | `#151311` | `#FFFFFF` | cards, botão de contorno, barra do cabeçalho |
| `--superficie-2` | `#1A1816` | `#F2ECE0` | trilhos, segmentados, chips neutros, área de conta dentro de card |
| `--superficie-3` | `#1F1C19` | `#DFD6C5` | item ativo sobre `--superficie-2` (valores aprovados mantidos) |
| `--borda` | `#2C2925` | `#E8E0D2` | borda de card e divisória |
| `--borda-campo` | `#65615A` | `#898174` | contorno de campo e de opção (≥ 3:1 sobre o fundo) |
| `--texto` | `#F0E9DD` | `#1A1714` | texto principal (16:1 / 16,9:1 no fundo) |
| `--texto-2` | `#A39C91` | `#6B6254` | texto secundário (7,1:1 / 5,7:1 no fundo) |
| `--texto-3` | `#6E675E` | `#A0957F` | **só** placeholder e enfeite (3,5:1 / 2,8:1) |
| `--acento` | `#E5533B` | `#C23B22` | botão principal, seleção, foco, dia de treino |
| `--acento-texto` | `#E5533B` | `#A53520` | acento como texto pequeno (5,2:1 / 6,3:1 no fundo) |
| `--acento-suave` | `#2A1512` | `#FBE7E1` | fundo de pílula e selo no acento |
| `--sobre-acento` | `#0F0E0D` | `#FFFFFF` | texto sobre o acento (5,2:1 / 5,3:1) |
| `--verde` | `#7BB878` | `#2F7A4A` | "já tenho", salvo, ajuste concluído, ritmo bom |
| `--erro` / `--erro-suave` | `#F07A62` / `#2A1512` | `#B23A24` / `#FBE4DE` | erro de campo, ação de apagar |
| `--logo-arroz` / `--logo-faixa` | `#FFFFFF` / `#14110F` | `#FFFFFF` / `#14110F` | as duas cores fixas do logo |
| `--ponto-grade` | `rgba(240,233,221,.07)` | `rgba(26,23,20,.09)` | pontinhos do fundo das telas de entrada |

**De onde vêm os nomes dos protótipos:** `--papel` → `--superficie`, `--creme` → `--superficie-2`, `--ease` → `--curva-saida`, `--mola` → `--curva-mola`, `--fonte` → `--fonte-corpo`. Os demais nomes já eram os da base.

### Migração dos tokens

Cada variável que `tokens.css` define hoje, e o que acontece com ela:

| Variável atual | Escuro | Washi | O que muda |
|---|---|---|---|
| `--superficie-hover` | `#151311` | `#FFFFFF` | fica igual à superfície: o hover de card passa a ser subida + `--sombra-leve` |
| `--linha` | `#3A3631` | `#D9CFBE` | borda de chip neutro, botão de contorno e etapa futura |
| `--borda-hover` | `#6E675E` | `#A0957F` | borda no hover (o mesmo tom do `--texto-3`) |
| `--borda-forte` | `#5C564D` | `#9A8E78` | sem mudança: sugestão em hover, barra negativa |
| `--divisor` | `#221F1C` | `#EFE8DC` | linhas internas de lista e tabela |
| `--grade` | — | — | **sai**: o quadriculado vira os pontinhos de `--ponto-grade` |
| `--texto-2-ativo` | `#A39C91` | `#645B4E` | sem mudança: secundário sobre `--superficie-3` |
| `--ponto` | `#7A7368` | `#7A6F5E` | sem mudança: pesagens no gráfico |
| `--erro-campo` | — | — | **vira `--erro`** |
| `--erro-fundo` | — | — | **vira `--erro-suave`** |
| `--erro-borda` | `#7A2E22` | `#E9B6A9` | borda de faixa de erro |
| `--sucesso-fundo` | `#151F14` | `#E9F1EC` | faixa de sucesso |
| `--sucesso-borda` | `#3F5A3A` | `#A9C9B4` | borda da faixa de sucesso |
| `--quimono*` | — | — | **saem**: o quimono em traço dá lugar ao ícone 3D |
| `--faixa-*` (branca, azul, roxa, marrom, preta) | — | — | **saem**: o progresso do onboarding passa a ser a linha de etapas (Conta, Perfil, Objetivo, Treino, Comida) no acento |
| `--fonte-titulo`, `--fonte-corpo` | Fustat | Fustat | as duas apontam para a mesma família |
| `--fonte-mono` | `ui-monospace, 'Cascadia Mono', Consolas, monospace` | igual | sai o IBM Plex Mono; fica só para o texto de compartilhar e o link de convite |
| `--t-*`, `--rastreio-rotulo` | — | — | novos valores em Typography |
| `--raio-*` | — | — | novos valores em Shapes |
| `--curva-saida`, `--curva-suave`, `--d-*` | — | — | ficam; entra `--curva-mola` (Motion) |
| `--largura`, `--margem` | — | — | 1280px e 1,5rem (1rem abaixo de 560px) |
| `--e-*`, `--altura-cabecalho`, `--altura-abas` | — | — | sem mudança |

Entram como novas: `--acento-suave`, `--verde`, `--erro`, `--erro-suave`, `--logo-arroz`, `--logo-faixa`, `--ponto-grade`, `--sombra`, `--sombra-leve`, `--sombra-icone` e `--curva-mola`.

**Sombras (tokens novos, nunca escritas direto):**

| Variável | Escuro | Washi | Uso |
|---|---|---|---|
| `--sombra` | `0 24px 60px -24px rgba(0,0,0,.7)` | `0 24px 60px -24px rgba(40,24,12,.22)` | folha lateral, mockups grandes |
| `--sombra-leve` | `0 10px 30px -14px rgba(0,0,0,.6)` | `0 10px 30px -14px rgba(40,24,12,.18)` | card em hover, card aberto, indicador da nav |
| `--sombra-icone` | `drop-shadow(0 16px 14px rgba(0,0,0,.6)) drop-shadow(0 3px 3px rgba(0,0,0,.45))` | `drop-shadow(0 14px 12px rgba(70,45,20,.2)) drop-shadow(0 3px 3px rgba(70,45,20,.12))` | todo ícone 3D |

O botão principal ganha uma sombra da própria cor (`0 10px 22px -12px var(--acento)`), que cresce no hover.

### Named Rules
**A Regra do Carimbo.** O vermelho marca, não pinta: botão principal, a opção escolhida, o dia de treino, o foco e uma palavra do título. Avatar e indicador deslizante podem ser cheios no acento porque são pequenos. Se uma área do tamanho de um card ficou vermelha, é carimbo demais.

**A Regra do Contraste Medido.** Texto que precisa ser lido usa `--texto` ou `--texto-2`. `--texto-3` não passa 4,5:1 e fica para placeholder e enfeite. Acento como texto pequeno usa `--acento-texto`. No escuro, o texto sobre o acento é escuro (`--sobre-acento`), porque branco sobre `#E5533B` dá 3,7:1. Os protótipos usam branco ali; no código vale o escuro. No Washi, o verde sobre `--superficie-2` fica um pouco abaixo de 4,5:1 (4,46): em texto pequeno, use o verde sobre `--superficie` ou `--fundo`. Pelo mesmo motivo, o acento sobre `--acento-suave` (4,47) usa `--acento-texto` nas pílulas do Washi.

**A Regra do Campo Visível.** O contorno de campo usa `--borda-campo` (3:1). A borda clara dos protótipos (`#D9CFBE` / `#4A443D`) fica em 1,5:1 e 2:1 e não serve para campo.

## Typography

**Uma família só: Fustat** (400, 500, 600, 700, 800), servida de `frontend/fontes/` como as fontes de hoje: a CSP de produção não libera o Google Fonts. Os fallbacks são `'Segoe UI', system-ui, -apple-system, sans-serif`. `--fonte-titulo` e `--fonte-corpo` apontam as duas para essa pilha.

Tamanhos em token: `--t-rotulo` 12px, `--t-pequeno` 13px, `--t-apoio` 14px, `--t-corpo` 15px, `--t-corpo-g` 16px, `--t-lead` 17px, `--t-h3` 20px, `--t-h2` 24px, `--t-h1` `clamp(2rem, 1.5rem + 1.6vw, 2.875rem)`, `--t-display` `clamp(2.75rem, 1.8rem + 4vw, 4.5rem)`, `--t-numero-g` `clamp(2.5rem, 2rem + 2vw, 3.375rem)`. `--rastreio-rotulo` passa a 0.08em.

### Hierarchy
- **Display** (800, 44→72px, `letter-spacing: -0.045em`): título do hero da landing.
- **Título de tela** (800, 32→46px, `-0.04em`): o `h1` da faixa de título, com uma palavra no acento (`<span>`): "Lista de **compras.**", "Sua **casa.**".
- **Seção** (800, 20–24px, `-0.02em`) e **título de card** (800, 18–20px).
- **Número grande** (800, 40→54px, `-0.055em`): meta do dia, peso de tendência, gasto em repouso. A unidade vem ao lado em 15–16px, peso 500 e `--texto-2`.
- **Corpo** (400, 15–16px, `line-height: 1.5`); textos de apoio em 13–14,5px.
- **Rótulo** (800, 12px, maiúsculas, `letter-spacing: 0.08em`, no `--acento-texto`): "SEUS TREINOS", "ORÇAMENTO POR SEMANA".

Títulos usam `text-wrap: balance`. Os campos têm no mínimo 16px, para o iOS não dar zoom.

### Named Rules
**A Regra do Algarismo Natural.** Os algarismos tabulares da Fustat são largos demais e abrem buracos nos números. Use `font-variant-numeric: normal`. Onde uma coluna precisa alinhar (tabela de pesagens, quantidades da lista de compras), alinhe o número à direita em vez de trocar os algarismos.

## Layout

- **Contêiner:** largura máxima de 1280px, margem lateral de 24px (16px abaixo de 560px), respeitando `env(safe-area-inset-*)`.
- **Cabeçalho:** fixo no topo, como uma barra flutuante em pílula (fundo `--superficie` translúcido com desfoque, borda e `--sombra-leve`). Tem o logo, a navegação em segmentado com um indicador branco que desliza para o item sob o mouse, o sol/lua e o avatar.
- **Faixa de título:** rótulo, `h1` com destaque e uma frase; as ações principais ficam à direita.
- **Grade:** coluna principal flexível e lateral de 360px (`gap` de 24px). A lateral fica **grudada** ao rolar (`position: sticky; top: 100px`) e vira coluna única abaixo de 1060px.
- **Celular** (≤ 860px): a navegação do topo dá lugar a 6 abas fixas embaixo, com ícone e fundo translúcido: Dieta, Progresso, Treino, Comida, Casa, Perfil. "Alimentação" vira "Comida" só nas abas. A aba ativa usa `--acento-suave` com o ícone no acento. A aba Casa leva o contador de pedidos (só para quem cuida da casa).
- **Telas de entrada** (login, cadastro, senha, onboarding): o formulário fica à esquerda, centralizado em até 420px. À direita, um painel de cantos grandes com cenas que se alternam (os ícones 3D orbitando, um mini-app com a refeição sendo marcada, o gráfico de peso) e a legenda. No celular, o painel some.

## Elevation & Depth

Três camadas: `--fundo` → `--superficie` (card) → `--superficie-2` (o que fica dentro do card). Cada card tem borda de 1px. A sombra entra para dizer "isto está acima" ou "isto reage":

- card em hover (`.ergue`): sobe 3px e ganha `--sombra-leve`;
- card aberto ou em foco: a borda puxa para o acento e ganha `--sombra-leve`;
- botão principal: a sombra é da cor do acento;
- folha lateral e diálogo: `--sombra`, com o fundo escurecido e levemente desfocado;
- ícone 3D: sempre `--sombra-icone`, que muda com o tema (marrom suave no Washi, preto no escuro).

### Named Rules
**A Regra da Sombra Quente.** Sombra só por token, nunca preta pura no Washi, nunca em texto. Nada de glow neon nem de várias sombras empilhadas num card.

## Shapes

- **Raios em token:** `--raio-1` 6px, `--raio` 14px (campos), `--raio-2` 18px (opções), `--raio-card` 24px, `--raio-pilula` 999px.
- **Cards:** 24px (20px no celular).
- **Opções, dias, sub-blocos dentro de card:** 16–18px.
- **Campos:** 14px; a caixa de peso tem 18px.
- **Botões, segmentados, chips, nav e abas:** pílula (999px).
- **Ícones de seção:** quadrado de 12–14px de raio com o símbolo em traço de 2px.
- **Gradiente:** permitido só nestes casos, todos funcionais:
  - a máscara que esmaece as bordas do carrossel e da faixa de pratos;
  - os padrões repetidos: o tracejado do "descanso" no gráfico de treino e as marcas de hora da régua de 24h;
  - o tampo redondo da mesa da Casa (um círculo em `--superficie-2`);
  - o leve degradê branco do papel da carta no envelope;
  - a amostra "Igual ao aparelho".

  Nunca como fundo decorativo de seção ou card.

## Motion

Duas curvas, em token:
- `--curva-saida: cubic-bezier(.22, .8, .24, 1)`: entradas, revelações, trocas de largura;
- `--curva-mola: cubic-bezier(.34, 1.36, .64, 1)` (nova): tudo que "encaixa", como indicadores, seleção, ícones e chips.

`--curva-suave` e as durações `--d-*` continuam.

**Padrões:**
- **Indicador deslizante (`.ind`):** em todo segmentado (objetivo, período, categoria, orçamento, sexo), na nav e nos dias. É uma pílula posicionada por `offsetLeft`/`offsetWidth`, que desliza com `--curva-mola` em 0,5–0,55s. Só se aplica a segmentado: em opções de card não.
- **Revelação em cascata (`.surge`):** os cards entram subindo 14px e aparecendo, com `transition-delay: calc(var(--i) * 70ms)`. Disparo por `setTimeout` (o visualizador de artifacts segura o `requestAnimationFrame`).
- **Contadores:** os números grandes contam até o valor em 0,5–0,7s.
- **Abrir e fechar:** detalhes, confirmações e cards expansíveis abrem pela linha da grade (`grid-template-rows: 0fr → 1fr`), sem `max-height`.
- **Seleção:** chip e opção marcados dão um pequeno salto (`scale`/`translateY` com `--curva-mola`); ícone 3D escolhido gira uns 6–8°.
- **Gráficos:** linhas se desenham uma vez, ao carregar ou ao trocar o período. **Nunca em laço.**
- **Enfeite em laço (lento, pausável no hover):** ícones flutuando, o ponto orbitando o avatar do Perfil, a faixa de pratos da Alimentação, o carrossel de modalidades e o envelope da senha abrindo e fechando.
- **Troca de tema:** cruzamento de 280ms por View Transition.
- **`prefers-reduced-motion: reduce`:** desliga animações e transições; tudo aparece no estado final (gráfico desenhado, envelope aberto, cards visíveis).

## Components

Em `frontend/css/componentes.css`; o estilo próprio de cada tela fica em `frontend/css/telas/`.

- **Logo:** a tigela de arroz com a faixa preta amarrada (SVG `<symbol id="marca">`, arroz em `--logo-arroz`, tigela no `--acento`, faixa em `--logo-faixa`), seguida de "ronu" em Fustat 800 minúsculo.
- **Ícones 3D:** objetos de massinha em creme (`#F2ECE0`) e vermelho-tijolo (`#C23B22`), luz de cima à esquerda e **sem sombra no chão**. São gerados no Figma, recortados para PNG/WebP com fundo transparente e usam `--sombra-icone`. O conjunto do sistema tem os da base (luva, bandagem, quimono, halter, óculos de natação, balança, calendário, sacola e envelope em camadas: aba, interior e bolso) e um para cada modalidade do catálogo. Ícone de modalidade nova segue o mesmo prompt e o mesmo recorte. Os arquivos ficam em `frontend/img/`; o prompt, o recorte (`tools/recorta-icone-3d.py`) e as camadas do envelope estão em `docs/icones-3d.md`.
- **Botões:**
  - primário: pílula no acento, com seta que anda no hover;
  - contorno: pílula em `--superficie` com borda;
  - redondo de 40px: sol/lua e fechar;
  - texto: link no acento.
  
  Carregando = `aria-busy` com um giro no lugar do rótulo; desabilitado usa `--superficie-2` com `--texto-3`. A ação perigosa usa `--erro`.
- **Campos:** `.campo` com rótulo acima (13,5–14px, 700). A caixa fica em `--fundo` com contorno `--borda-campo`; o foco usa a borda no acento e um halo de 4px do acento a 14%. Erro: `aria-invalid`, a caixa treme 0,35s e a mensagem aparece em `--erro`. Senha tem botão de olho e medidor de força em 4 barras.
- **Escolhas:**
  - `.seg`: segmentado com indicador deslizante; o selecionado fica com `--sobre-acento`;
  - `.opcao`: card de rádio com ícone, título e dica; o marcado tem borda no acento e fundo com 6% de acento;
  - chips: "gosto" no verde, "não como" no acento, "evitar" tracejado, sugestão tracejada com "+", alergia como caixa de marcar;
  - `.dias`: 7 botões.
- **Cards:** `.cartao` (24px de raio, 22px de preenchimento) com rótulo ou título no topo. Card expansível: o cabeçalho é um botão com seta que gira, e o corpo abre pela grade.
- **Pílulas:** `--acento-suave` com texto `--acento-texto` (ex.: "−3%", "da casa", "Treino mudou").
- **Avisos:** o sucesso é uma faixa em verde suave; o erro, uma faixa em `--erro-suave`; a nota fica em `--superficie-2`. "Quando muda na dieta" é o card que aparece com um selo e um botão pulsando quando algo mudou.
- **Dados:**
  - anel de meta;
  - trilhos de macro;
  - barras empilhadas por dia, com a modalidade aberta no acento e as outras em `--texto-3`;
  - gráfico de peso com os pontos das pesagens, a linha de tendência e uma dica ao passar o mouse;
  - régua de 24h com a janela acordada e o treino.
- **Folha:** sai da direita (diálogo em largura de 440px, tela cheia no celular). Usada para apagar a conta e para trocar alimento.
- **Carrossel:** faixa horizontal com `scroll-snap`, bordas esmaecidas, setas redondas e passagem automática. Pausa no hover e por 6s depois de um toque ou de um foco, sem travar para sempre.

## Do's and Don'ts

### Do:
- **Do** usar só as variáveis de `tokens.css`, sombras incluídas, e conferir cada tela nos dois modos.
- **Do** dar a todo controle um botão ou link de verdade, um rótulo visível ou `aria-label`, e foco visível (contorno de 2px no acento).
- **Do** mostrar a conta por trás de cada número: o gasto da sessão, o gasto em repouso parte a parte, o "Exato → arredondado" da lista de compras.
- **Do** usar o botão oficial do Google (`renderButton`), redesenhado no tema certo quando o modo troca.
- **Do** pôr `[hidden] { display: none !important }` na base: `display: grid` em componente não pode vencer o `hidden`.

### Don't:
- **Don't** escrever cor, sombra ou fonte direto em tela nenhuma.
- **Don't** usar `--texto-3` em texto que precisa ser lido, nem texto branco sobre o acento no modo escuro.
- **Don't** usar algarismos tabulares na Fustat.
- **Don't** animar gráfico em laço, nem animar sem um estado final para o movimento reduzido.
- **Don't** usar gradiente como fundo, glow ou sombra preta no Washi.
- **Don't** voltar ao logo 浪, ao carimbo 浪人 ou ao quimono em traço: a marca agora é a tigela com a faixa.
- **Don't** inventar dados, depoimentos ou modalidades: tudo que é número ou catálogo vem da API.
