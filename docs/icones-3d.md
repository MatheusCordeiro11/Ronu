# Ícones 3D e ilustrações

As imagens do redesign ficam em `frontend/img/`. Todas foram geradas no Figma (arquivo "Ronu — Redesign da landing",
chave `jiH6eEEN43irHMPtkn4TKn`, modelo `gpt-image-2.5-flare`) e recortadas para WebP com fundo transparente. Os
originais em resolução cheia ficam nesse arquivo do Figma; o repositório guarda só os recortes.

## Onde está cada uma

| Pasta | Conteúdo |
|---|---|
| `img/icones/` | os ícones da base: balança, bandagem, calendário, halter, luva, óculos de natação, quimono, sacola |
| `img/modalidades/` | um ícone por modalidade que não usa um da base |
| `img/envelope/` | o envelope das telas de senha, em camadas: `aba`, `interior`, `bolso` e a `chave` da carta |
| `img/landing/` | as ilustrações em gravura da landing: `mao-faixa`, `mao-tigela` e `onda` |

Cinco modalidades usam um ícone da base: Jiu-jitsu → `quimono`, Muay thai → `bandagem`, Boxe → `luva`,
Musculação → `halter` e Natação → `oculos`. As outras 21 têm o arquivo com o nome da modalidade, sem acento
(`beach` é o Beach tennis, `funcional` é o Treino funcional).

## Como gerar um ícone novo

Use este prompt, trocando só a descrição do objeto e onde vai o vermelho:

> A single 3D icon of **[objeto, com os detalhes que o identificam]**, soft matte clay render, warm cream / off-white
> (#F2ECE0) **[parte creme]** with muted brick red (#C23B22) **[parte vermelha]**, smooth rounded friendly shapes,
> soft diffused studio lighting from the top left, gentle ambient occlusion on the object itself, three-quarter view,
> centered with generous empty margin, floating in the air. Absolutely no shadow on the ground, no contact shadow,
> no cast shadow, no reflection. Minimal premium app-icon style like modern fintech websites. Isolated on a plain flat
> pure white background, no text, no other objects.

O "sem sombra no chão" importa: a sombra do render é bege e opaca, e no modo escuro vira um halo claro. A sombra
certa entra pelo CSS, com `--sombra-icone`, que muda com o tema.

Depois de gerar, baixe a imagem em resolução cheia (o link do Figma vence em 7 dias) e recorte:

```
python tools/recorta-icone-3d.py ícone-bruto.png frontend/img/modalidades/nome.webp
```

Confira o recorte sobre o fundo escuro (`#151311`) e sobre o claro (`#FBF8F3`) antes de usar.

## O envelope em camadas

O envelope é um render só (aberto, vazio e de frente, com a dobradiça na horizontal) separado em camadas na mesma
moldura de 320 px, para a aba poder girar:

- **aba**: tudo acima da dobradiça. A base da aba, que no render ficava atrás do interior, foi preenchida com a cor
  da aba, senão o envelope fechado mostraria um buraco;
- **interior**: a parte bege escura de dentro;
- **bolso**: a frente, com o V;
- **chave**: tirada de outro render; a carta em si é desenhada no CSS (um cartão branco).

A dobradiça fica a **43,4%** da altura da moldura. A aba gira com `transform-origin: 50% 43.4%` e `rotateX(-180deg)`
quando fechada; enquanto está dobrada, fica na frente do bolso, e passa para trás da carta quando fica de lado.
O interior acima da dobradiça é cortado (`clip-path`) enquanto o envelope está fechado.
