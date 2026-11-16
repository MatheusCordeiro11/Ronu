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
| `img/landing/` | as ilustrações em gravura da landing: `mao-faixa`, `mao-tigela`, `onda` e uma mão por modalidade da faixa (`mao-muay-thai`, `mao-boxe`, `mao-musculacao`, `mao-corrida`, `mao-futebol`, `mao-natacao`, `mao-ciclismo`, `mao-volei`, `mao-danca`) |
| `img/mascote/` | o mascote ninja da landing, de frente e **sem os olhos** (`ninja.webp`, 185 × 300): os olhos são SVG no `index.html`, para seguirem o mouse |

O ninja foi gerado no Figma no estilo **riso** (impressão em duas tintas, vermelho-carimbo e azul-petróleo, com
grão e meio-tom; pele negra feita pela sobreposição das duas tintas, rosto coberto até o nariz). Os olhos foram
apagados pintando a pele por cima, interpolada linha a linha entre os dois lados de cada olho, com ruído para
imitar o grão; a máscara não foi tocada. No desenho de 185 × 300, os olhos ficam em (43,5; 78,4) e (90,3; 78,4),
com raio de 11,8 × 11,5, e a cintura (onde o pulo parte o desenho) fica na linha 226.

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

## As mãos da faixa de modalidades

Na landing, escolher uma modalidade na faixa troca a mão da esquerda (a da faixa preta, `mao-faixa`, é o
jiu-jitsu). As outras nove foram geradas em nov/2026 com este prompt, trocando só o objeto pendurado no pulso:

> Close-up hand-colored engraving illustration, detailed etching with dense fine cross-hatching, like a vintage
> anatomical print colored with warm watercolor. A thick, muscular, veiny forearm of **[quem treina]** enters from the
> left edge of the image (cut off by the frame) and the large hand fills the right half of the image: wrist slightly
> raised, back of the hand facing the viewer, palm facing down, fingers relaxed and curled loosely, index and middle
> fingers pointing down and to the right, as if about to take something. Deep tanned bronze skin with ochre, sienna
> and burnt orange hatching, prominent tendons and knuckles, weathered. **[objeto, em muted brick red (#C23B22),
> pendurado no pulso por uma alça enrolada duas vezes, drawn with the same engraved hatching]**. Exactly one arm.
> The arm and hand fill most of the frame. Isolated on a plain flat pure white background, no outline around the
> figure, no shadow, no text, no other objects.

Exceções: a dança é uma mão feminina (slender, graceful feminine forearm of a ballerina), e a musculação segura o
halter com a mão fechada (hammer grip), porque pendurado ele ficava estranho.

O `recorta-icone-3d.py` não serve para elas: o fundo branco preso entre os dedos fica no recorte. Elas foram
recortadas tirando também as manchas brancas internas grandes (a partir de 400 px) e encolhendo o alfa 1 px para
não sobrar halo. Depois, 24% da largura do lado esquerdo (o braço) foi cortado e a imagem ampliada de novo para
900 px de largura, para a mão ficar do tamanho da `mao-tigela`; a do halter não tem esse corte.

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
