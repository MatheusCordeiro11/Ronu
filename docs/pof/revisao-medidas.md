# Revisão das medidas caseiras: item da base → linha da POF → gramas

Fonte, método e regras: ver `README.md` desta pasta. Aqui, o resultado para você revisar. **Nada disto está
no código.** Cada linha "direta" tem a página do PDF da POF; a descrição da fonte de cada medida está em
`medidas-por-item.csv`.

## Resumo dos 121 itens

| Situação | Itens |
|---|---|
| Têm ao menos uma medida **direta** | 97 |
| Só medida **semelhante** (outro preparo), não usada por padrão | 10 |
| Alimento mapeado, mas **nenhuma** medida direta nem semelhante | 9 |
| **Sem correspondência** na POF | 5 |
| **Só gramas** (soma das três últimas linhas) | **24** |

Total: 315 medidas diretas e 52 semelhantes.

**Só gramas na revisão original (24; hoje são 25, ver `tabela-2a-121-itens.md`):**
- Sem correspondência: coxão mole, clara de ovo, queijo parmesão, amendoim torrado, chá mate.
- Mapeado, sem medida: tapioca, pão de forma integral*, cará, manga palmer, coxa de frango, bisteca suína,
  pernil suíno, pasta de amendoim, azeite de oliva.
- Só semelhante: espinafre refogado, berinjela cozida, lagarto, peito de frango (grelhado e cozido),
  tilápia, salmão, sardinha, merluza, corvina.

\* Pão de forma integral continua com a fatia de 25 g da fase 1 (IBGE: "Pão de forma de aveia - unidade"),
que a extração confirma, mas a fonte é um pão de forma de aveia, não integral.

## Decisões de 2026-10-20 (aplicadas na fase 2a)

1. **Medida semelhante (outro preparo):** o filé de 120 g do "peixe cozido" entra, com origem semelhante, só
   para tilápia, merluza e corvina (se comem em filé). Salmão (posta) e sardinha (inteira) ficam só em
   gramas. Frango: as medidas de frito e assado **não** entram; peito e coxa só gramas.
2. **Itens genéricos:** entra a medida da categoria quando ela é o mesmo alimento com outro nome (feijão,
   maçã, mamão, "aipim" para mandioca, "bife bovino" para patinho, alcatra, contrafilé, maminha e filé
   mignon; músculo e acém ficam só em gramas). Quando a categoria mistura
   alimentos diferentes (peixe, queijo minas padrão para frescal), só gramas. Toda medida assim tem
   `OrigemMedida.Categoria`; a lista está em `tabela-2a-121-itens.md`.
3. **Valores estranhos:** batata-doce *unidade* 355 g e lombo *fatia* 190 g ficam **em espera** (extração
   conferida contra o texto do PDF, mas bem fora do usual); os demais valores observados são plausíveis.

## O protótipo da troca contra os dados reais

| No protótipo | Na POF |
|---|---|
| Pão francês, 2 unidades (100 g) | Unidade 50 g: **confere** |
| Ovo, 2 ovos (100 g) | Unidade 45 g (cozido) e 50 g (frito): **confere** |
| Banana prata, 2 unidades (150 g) | Unidade 75 g: **confere** |
| Arroz, 6 colheres de servir (300 g) | Colher de arroz/servir 45 g (6,7 colheres), concha 100 g: **tem medida** |
| Feijão, 1 concha (150 g) | Concha 140 g: **tem medida** |
| Aveia, 4 colheres de sopa (40 g) | Colher de sopa 15 g (2,7 colheres): **tem medida, com outro valor** |
| Peito de frango, 2 filés médios (200 g) | Só semelhante (Filé 100 g, medido frito): **sem medida direta** |
| Tilápia, 2 filés (246 g) | Só semelhante (Filé 120 g, peixe cozido): **sem medida direta** |
| Patinho moído, 1 concha e meia (178 g) | Colher de arroz/servir 60 g e colher de sopa 25 g; **sem concha direta** |
| Azeite, 1 colher de sopa (15 g) | **Sem medida** (a POF só empresta a do óleo vegetal, 8 g) |
| Coxa de frango, 2 coxas (221 g) | **Sem medida direta** |

Os números do protótipo são ilustrativos; os da coluna da direita são os que a base teria.

## Itens, um a um

- **Arroz branco cozido** (`arroz-branco-cozido`) ← POF 6300101 Arroz (polido, parboilizado), preparo 99
  - direta: Colher de sopa 25 g; Colher de arroz/servir 45 g; Concha 100 g; Escumadeira 85 g; Prato fundo 200 g (pág. 35)
  - nota: POF "Arroz": as fontes descrevem arroz cozido.
- **Arroz integral cozido** (`arroz-integral-cozido`) ← POF 6300201 Arroz integral, preparo 99
  - direta: Colher de sopa 20 g; Colher de arroz/servir 63 g; Concha 117 g; Escumadeira 59 g; Prato fundo 200 g (pág. 36)
- **Aveia em flocos** (`aveia-em-flocos`) ← POF 6500401 Aveia em flocos, preparo 99
  - direta: Colher de sopa 15 g; Colher de sobremesa 7 g; Colher de chá 2 g (pág. 74)
- **Macarrão cozido** (`macarrao-cozido`) ← POF 6503401 Macarrão, preparo 2
  - direta: Colher de sopa 25 g; Colher de arroz/servir 50 g; Escumadeira 110 g; Pegador 110 g; Garfada 30 g; Prato raso 320 g; Prato fundo 400 g (pág. 87)
- **Cuscuz de milho cozido** (`cuscuz-de-milho-cozido`) ← POF 6902901 Cuscuz, preparo 99
  - direta: Pedaço 135 g (pág. 193)
- **Milho verde em conserva** (`milho-verde-em-conserva`) ← POF 7700401 Milho verde em conserva, preparo 99
  - direta: Colher de sopa 24 g; Colher de sobremesa 17 g (pág. 312)
- **Tapioca** (`tapioca`) ← POF 6501516 Tapioca de goma, preparo 99
  - nenhuma medida direta nem semelhante: só gramas
  - nota: POF "Tapioca de goma" (a tapioca pronta).
- **Farinha de mandioca torrada** (`farinha-de-mandioca-torrada`) ← POF 6501401 Farinha de mandioca, preparo 99
  - direta: Colher de sopa 16 g; Colher de sobremesa 10 g; Colher de chá 5 g; Colher de arroz/servir 33 g; Concha 110 g; Punhado 23 g; Prato fundo 271 g; Tigela 148 g; Copo médio 172 g; Xícara de chá 148 g; Caneca 171 g (pág. 80)
- **Pão francês** (`pao-frances`) ← POF 8000105 Pão de sal, preparo 99
  - direta: Unidade 50 g (pág. 393)
  - nota: POF "Pão de sal" (o pão francês).
- **Pão de forma integral** (`pao-de-forma-integral`) ← POF 8001401 Pão integral, preparo 99
  - nenhuma medida direta nem semelhante: só gramas
  - nota: POF "Pão integral".
- **Pão de queijo assado** (`pao-de-queijo-assado`) ← POF 8000801 Pão de queijo, preparo 99
  - direta: Unidade 20 g; Unidade pequena 10 g (pág. 394)
- **Feijão cozido** (`feijao-cozido`) ⚠ genérico ← POF 6303102 Feijão (preto, mulatinho, roxo, rosinha, etc.), preparo 99
  - direta: Colher de sopa 17 g; Colher de arroz/servir 35 g; Concha 140 g (pág. 43)
  - nota: POF só tem "Feijão (preto, mulatinho, roxo, rosinha, etc.)": não separa o carioca.
- **Feijão preto cozido** (`feijao-preto-cozido`) ← POF 6303102 Feijão (preto, mulatinho, roxo, rosinha, etc.), preparo 99
  - direta: Colher de sopa 17 g; Colher de arroz/servir 35 g; Concha 140 g (pág. 43)
- **Feijão fradinho cozido** (`feijao-fradinho-cozido`) ⚠ genérico ← POF 6301603 Feijão de corda, preparo 99
  - direta: Colher de sopa 17 g; Colher de arroz/servir 35 g; Concha 140 g (pág. 40)
  - nota: POF "Feijão de corda" (fradinho = feijão-de-corda, nome regional).
- **Lentilha cozida** (`lentilha-cozida`) ← POF 6302901 Lentilha, preparo 99
  - direta: Colher de sopa 18 g; Colher de arroz/servir 32 g; Concha 160 g (pág. 42)
- **Batata inglesa cozida** (`batata-inglesa-cozida`) ← POF 6400101 Batata-inglesa, preparo 2
  - direta: Unidade 140 g; Unidade pequena 70 g; Colher de sopa 30 g; Colher de arroz/servir 60 g; Escumadeira 80 g (pág. 45)
- **Batata doce cozida** (`batata-doce-cozida`) ← POF 6400401 Batata-doce, preparo 2
  - direta: Unidade 355 g; Fatia 70 g; Colher de sopa 42 g (pág. 51)
- **Mandioca cozida** (`mandioca-cozida`) ← POF 6400601 Mandioca, preparo 2
  - direta: Pedaço 100 g; Colher de sopa 30 g; Colher de arroz/servir 60 g (pág. 54)
- **Mandioquinha cozida** (`mandioquinha-cozida`) ← POF 6400303 Mandioquinha salsa (Batata-baroa), preparo 2
  - direta: Unidade 80 g; Colher de sopa 35 g (pág. 50)
  - nota: POF "Mandioquinha salsa (Batata-baroa)".
- **Cará cozido** (`cara-cozido`) ← POF 6400701 Cará, preparo 2
  - nenhuma medida direta nem semelhante: só gramas
- **Alface crua** (`alface-crua`) ← POF 6700101 Alface, preparo 99
  - direta: Unidade 200 g; Unidade pequena 130 g; Colher de sopa 8 g; Folha 10 g; Pires 20 g; Prato de sobremesa 30 g; Prato raso 50 g; Prato fundo 60 g (pág. 104)
- **Rúcula crua** (`rucula-crua`) ← POF 6702001 Rúcula, preparo 99
  - direta: Ramo 6 g (pág. 124)
- **Agrião cru** (`agriao-cru`) ← POF 6701301 Agrião, preparo 1
  - direta: Colher de sopa 7 g; Garfada 30 g; Ramo 5 g; Pires 10 g; Prato raso 80 g (pág. 116)
  - semelhante (outro preparo, não usada): Colher de arroz/servir 50 g
- **Couve crua** (`couve-crua`) ← POF 6700501 Couve, preparo 1
  - direta: Folha 20 g (pág. 106)
  - semelhante (outro preparo, não usada): Colher de sopa 20 g
- **Tomate cru** (`tomate-cru`) ← POF 6705101 Tomate, preparo 99
  - direta: Unidade 100 g; Unidade pequena 50 g; Fatia 15 g; Colher de sopa 30 g (pág. 143)
- **Pepino cru** (`pepino-cru`) ← POF 6704001 Pepino, preparo 99
  - direta: Unidade 100 g; Colher de sopa 18 g (pág. 132)
- **Cenoura crua** (`cenoura-crua`) ← POF 6401201 Cenoura, preparo 1
  - direta: Unidade 120 g; Unidade pequena 55 g; Colher de sopa 12 g (pág. 68)
  - semelhante (outro preparo, não usada): Fatia 5 g; Escumadeira 48 g
- **Cenoura cozida** (`cenoura-cozida`) ← POF 6401201 Cenoura, preparo 2
  - direta: Unidade 100 g; Unidade pequena 46 g; Fatia 5 g; Escumadeira 48 g (pág. 69)
- **Beterraba crua** (`beterraba-crua`) ← POF 6401101 Beterraba, preparo 1
  - direta: Unidade 140 g; Colher de sopa 16 g; Prato de sobremesa 168 g (pág. 66)
  - semelhante (outro preparo, não usada): Fatia 12 g; Colher de arroz/servir 38 g
- **Beterraba cozida** (`beterraba-cozida`) ← POF 6401101 Beterraba, preparo 2
  - direta: Unidade 125 g; Unidade pequena 75 g; Fatia 12 g; Colher de sopa 20 g; Colher de arroz/servir 38 g; Escumadeira 45 g; Prato de sobremesa 168 g (pág. 66)
- **Brócolis cozido** (`brocolis-cozido`) ← POF 6701704 Brócolis, preparo 2
  - direta: Colher de sopa 10 g; Pires 75 g (pág. 121)
- **Couve-flor cozida** (`couve-flor-cozida`) ← POF 6700601 Couve-flor, preparo 2
  - direta: Unidade pequena 30 g; Colher de sopa 25 g; Ramo 60 g; Prato de sobremesa 100 g (pág. 110)
- **Abobrinha cozida** (`abobrinha-cozida`) ← POF 6703701 Abobrinha, preparo 2
  - direta: Unidade 335 g; Fatia 61 g; Pedaço 17 g; Colher de sopa 30 g; Colher de arroz/servir 70 g; Escumadeira 90 g; Rodela 18 g (pág. 127)
- **Abobrinha refogada** (`abobrinha-refogada`) ← POF 6703701 Abobrinha, preparo 7
  - direta: Unidade 335 g; Pedaço 17 g; Rodela 18 g (pág. 129)
  - semelhante (outro preparo, não usada): Colher de sopa 30 g; Colher de arroz/servir 70 g; Escumadeira 90 g
- **Chuchu cozido** (`chuchu-cozido`) ← POF 6704101 Chuchu, preparo 2
  - direta: Unidade 230 g; Unidade pequena 145 g; Pedaço 30 g; Colher de sopa 20 g; Colher de arroz/servir 45 g; Escumadeira 110 g (pág. 133)
- **Couve refogada** (`couve-refogada`) ← POF 6700501 Couve, preparo 7
  - direta: Colher de sopa 20 g; Folha 25 g (pág. 107)
- **Espinafre refogado** (`espinafre-refogado`) ← POF 6700701 Espinafre, preparo 7
  - semelhante (outro preparo, não usada): Colher de sopa 25 g
- **Repolho cru** (`repolho-cru`) ← POF 6700901 Repolho, preparo 1
  - direta: Colher de sopa 10 g; Colher de arroz/servir 25 g; Escumadeira 25 g; Folha 26 g; Pires 100 g (pág. 113)
- **Repolho roxo cru** (`repolho-roxo-cru`) ⚠ genérico ← POF 6700901 Repolho, preparo 1
  - direta: Colher de sopa 10 g; Colher de arroz/servir 25 g; Escumadeira 25 g; Folha 26 g; Pires 100 g (pág. 113)
  - nota: POF "Repolho" não separa a cor.
- **Acelga crua** (`acelga-crua`) ← POF 6701501 Acelga, preparo 1
  - direta: Colher de sopa 6 g; Colher de arroz/servir 10 g; Folha 10 g (pág. 118)
- **Cebola crua** (`cebola-crua`) ← POF 6705701 Cebola, preparo 1
  - direta: Unidade 70 g; Unidade pequena 30 g; Fatia 6 g; Colher de sopa 10 g (pág. 148)
- **Abóbora cozida** (`abobora-cozida`) ← POF 6703901 Abóbora, preparo 2
  - direta: Pedaço 50 g; Colher de sopa 36 g; Escumadeira 100 g (pág. 130)
- **Berinjela cozida** (`berinjela-cozida`) ← POF 6705401 Berinjela, preparo 2
  - semelhante (outro preparo, não usada): Fatia 20 g; Colher de sopa 25 g; Colher de arroz/servir 75 g; Escumadeira 125 g
- **Banana prata** (`banana-prata`) ← POF 6801101 Banana (ouro, prata, d´água, da terra, etc.), preparo 99
  - direta: Unidade 75 g; Pedaço 16 g; Prato raso 235 g (pág. 155)
  - nota: POF "Banana (ouro, prata, d´água, da terra, etc.)".
- **Banana nanica** (`banana-nanica`) ⚠ genérico ← POF 6801101 Banana (ouro, prata, d´água, da terra, etc.), preparo 99
  - direta: Pedaço 16 g; Prato raso 235 g (pág. 155)
  - nota: POF junta todas as bananas; nanica = d´água.
- **Maçã fuji** (`maca-fuji`) ⚠ genérico ← POF 6803001 Maçã, preparo 99
  - direta: Unidade 150 g; Unidade pequena 90 g (pág. 160)
  - nota: POF "Maçã" não separa a variedade.
- **Pera williams** (`pera-williams`) ⚠ genérico ← POF 6803601 Pera, preparo 99
  - direta: Unidade 130 g (pág. 163)
  - nota: POF "Pera" não separa a variedade.
- **Laranja pera** (`laranja-pera`) ← POF 6801801 Laranja (pera, seleta, lima, da terra, etc.), preparo 99
  - direta: Unidade 180 g; Unidade pequena 90 g (pág. 156)
  - nota: POF "Laranja (pera, seleta, lima, da terra, etc.)".
- **Tangerina** (`tangerina`) ← POF 6802201 Tangerina, preparo 99
  - direta: Unidade 135 g; Unidade pequena 100 g (pág. 157)
- **Kiwi** (`kiwi`) ← POF 6807801 Kiwi, preparo 99
  - direta: Unidade 94 g; Rodela 11 g (pág. 174)
- **Mamão papaia** (`mamao-papaia`) ⚠ genérico ← POF 6803101 Mamão, preparo 99
  - direta: Unidade 310 g; Unidade pequena 270 g; Fatia 170 g; Colher de sopa 40 g; Colher de arroz/servir 70 g (pág. 160)
  - nota: POF "Mamão" não separa papaia e formosa.
- **Mamão formosa** (`mamao-formosa`) ⚠ genérico ← POF 6803101 Mamão, preparo 99
  - direta: Fatia 170 g (pág. 160)
  - nota: POF "Mamão" não separa papaia e formosa.
- **Abacaxi** (`abacaxi`) ← POF 6802601 Abacaxi, preparo 99
  - direta: Unidade 750 g; Unidade pequena 480 g; Fatia 75 g (pág. 158)
- **Melancia** (`melancia`) ← POF 6803401 Melancia, preparo 99
  - direta: Fatia 200 g; Pedaço 150 g (pág. 162)
- **Melão** (`melao`) ← POF 6803501 Melão, preparo 99
  - direta: Unidade 660 g; Fatia 90 g (pág. 162)
- **Morango** (`morango`) ← POF 6805201 Morango, preparo 99
  - direta: Unidade 12 g; Unidade pequena 7 g (pág. 169)
- **Uva itália** (`uva-italia`) ⚠ genérico ← POF 6803901 Uva, preparo 99
  - direta: Unidade 8 g; Unidade pequena 4 g (pág. 164)
  - nota: POF "Uva" não separa a variedade.
- **Manga palmer** (`manga-palmer`) ⚠ genérico ← POF 6803201 Manga, preparo 99
  - nenhuma medida direta nem semelhante: só gramas
  - nota: POF "Manga" não separa a variedade.
- **Abacate** (`abacate`) ← POF 6802701 Abacate, preparo 99
  - direta: Unidade 430 g; Unidade pequena 370 g; Colher de sopa 45 g (pág. 159)
- **Goiaba vermelha** (`goiaba-vermelha`) ⚠ genérico ← POF 6804201 Goiaba, preparo 99
  - direta: Unidade 170 g; Unidade pequena 50 g (pág. 165)
  - nota: POF "Goiaba" não separa a cor.
- **Açaí sem açúcar** (`acai-sem-acucar`) ← POF 6601706 Açaí, preparo 99
  - direta: Concha 101 g; Tigela 202 g; Copo médio 273 g; Copo grande 326 g (pág. 101)
- **Patinho bovino grelhado** (`patinho-bovino-grelhado`) ← POF 7100501 Patinho, preparo 3
  - direta: Bife 100 g (pág. 229)
  - semelhante (outro preparo, não usada): Pedaço 35 g
- **Patinho bovino moído refogado** (`patinho-bovino-moido-refogado`) ⚠ genérico ← POF 7104301 Carne moída, preparo 7
  - direta: Colher de sopa 25 g; Colher de arroz/servir 60 g (pág. 260)
  - nota: POF "Carne moída" (não separa o corte), refogada.
- **Carne moída de acém cozida** (`carne-moida-de-acem-cozida`) ⚠ genérico ← POF 7104301 Carne moída, preparo 2
  - direta: Colher de sopa 25 g; Colher de arroz/servir 60 g (pág. 259)
  - nota: POF "Carne moída" (não separa o corte), cozida.
- **Acém bovino cozido** (`acem-bovino-cozido`) ← POF 7100801 Acém, preparo 2
  - direta: Bife 100 g (pág. 233)
  - semelhante (outro preparo, não usada): Pedaço 35 g; Colher de arroz/servir 70 g
- **Alcatra bovina grelhada** (`alcatra-bovina-grelhada`) ← POF 7100301 Alcatra, preparo 3
  - direta: Bife 100 g (pág. 225)
  - semelhante (outro preparo, não usada): Pedaço 35 g
- **Contrafilé bovino grelhado** (`contrafile-bovino-grelhado`) ← POF 7100201 Contrafilé, preparo 3
  - direta: Bife 100 g (pág. 222)
  - semelhante (outro preparo, não usada): Pedaço 35 g
- **Coxão mole bovino cozido** (`coxao-mole-bovino-cozido`): sem alimento correspondente na POF. Só gramas.
- **Lagarto bovino cozido** (`lagarto-bovino-cozido`) ← POF 7106401 Lagarto bovino, preparo 2
  - semelhante (outro preparo, não usada): Pedaço 35 g
- **Músculo bovino cozido** (`musculo-bovino-cozido`) ← POF 7101001 Músculo bovino, preparo 2
  - direta: Bife 100 g (pág. 235)
  - semelhante (outro preparo, não usada): Pedaço 35 g; Colher de sopa 30 g
- **Maminha bovina grelhada** (`maminha-bovina-grelhada`) ← POF 7100303 Maminha, preparo 3
  - direta: Bife 100 g (pág. 226)
  - semelhante (outro preparo, não usada): Pedaço 35 g
- **Filé mignon grelhado** (`file-mignon-grelhado`) ← POF 7100101 Filé mignon, preparo 3
  - direta: Unidade 100 g; Bife 100 g (pág. 220)
  - semelhante (outro preparo, não usada): Pedaço 35 g
- **Peito de frango sem pele grelhado** (`peito-de-frango-sem-pele-grelhado`) ← POF 7800401 Peito de galinha ou frango, preparo 3
  - semelhante (outro preparo, não usada): Filé 100 g; Peito 180 g
- **Peito de frango sem pele cozido** (`peito-de-frango-sem-pele-cozido`) ← POF 7800401 Peito de galinha ou frango, preparo 2
  - semelhante (outro preparo, não usada): Filé 100 g; Peito 180 g; Colher de sopa 20 g
- **Coxa de frango sem pele cozida** (`coxa-de-frango-sem-pele-cozida`) ⚠ genérico ← POF 7800302 Frango em pedaços, preparo 2
  - nenhuma medida direta nem semelhante: só gramas
  - nota: POF "Frango em pedaços" (medidas coxa e sobrecoxa).
- **Sobrecoxa de frango sem pele assada** (`sobrecoxa-de-frango-sem-pele-assada`) ⚠ genérico ← POF 7800302 Frango em pedaços, preparo 4
  - direta: Sobrecoxa 65 g (pág. 329)
  - nota: POF "Frango em pedaços" (medidas coxa e sobrecoxa).
- **Frango sem pele assado** (`frango-sem-pele-assado`) ← POF 7800103 Frango inteiro, preparo 4
  - direta: Sobrecoxa 65 g; Peito 180 g; Asa 40 g (pág. 323)
  - nota: POF "Frango inteiro".
- **Lombo suíno assado** (`lombo-suino-assado`) ← POF 7103701 Lombo suíno, preparo 4
  - direta: Fatia 190 g (pág. 255)
- **Bisteca suína grelhada** (`bisteca-suina-grelhada`) ← POF 7103305 Bisteca suína, preparo 3
  - nenhuma medida direta nem semelhante: só gramas
- **Pernil suíno assado** (`pernil-suino-assado`) ← POF 7103401 Pernil suíno, preparo 4
  - nenhuma medida direta nem semelhante: só gramas
- **Filé de tilápia grelhado** (`file-de-tilapia-grelhado`) ⚠ genérico ← POF 7400101 Peixe de água doce (inteiro, em posta, em filé, etc.), preparo 3
  - semelhante (outro preparo, não usada): Unidade pequena 150 g; Filé 120 g; Posta 200 g
  - nota: POF "Peixe de água doce": a tilápia está nesta categoria; sem item próprio.
- **Salmão grelhado** (`salmao-grelhado`) ⚠ genérico ← POF 7200101 Peixe de mar (inteiro, em posta, em filé, etc.), preparo 3
  - semelhante (outro preparo, não usada): Filé 120 g; Posta 200 g
  - nota: POF "Peixe de mar": sem item próprio para salmão fresco.
- **Sardinha assada** (`sardinha-assada`) ⚠ genérico ← POF 7200101 Peixe de mar (inteiro, em posta, em filé, etc.), preparo 4
  - semelhante (outro preparo, não usada): Unidade pequena 150 g; Filé 120 g; Posta 200 g; Colher de sopa 20 g
  - nota: POF "Peixe de mar": sem item próprio para sardinha fresca.
- **Atum em conserva em água** (`atum-em-conserva-em-agua`) ← POF 7703402 Atum em conserva, preparo 99
  - direta: Colher de sopa 16 g; Colher de sobremesa 10 g (pág. 320)
- **Merluza assada** (`merluza-assada`) ⚠ genérico ← POF 7200101 Peixe de mar (inteiro, em posta, em filé, etc.), preparo 4
  - semelhante (outro preparo, não usada): Unidade pequena 150 g; Filé 120 g; Posta 200 g; Colher de sopa 20 g
  - nota: POF "Peixe de mar": sem item próprio para merluza.
- **Corvina assada** (`corvina-assada`) ⚠ genérico ← POF 7200101 Peixe de mar (inteiro, em posta, em filé, etc.), preparo 4
  - semelhante (outro preparo, não usada): Unidade pequena 150 g; Filé 120 g; Posta 200 g; Colher de sopa 20 g
  - nota: POF "Peixe de mar": sem item próprio para corvina.
- **Ovo de galinha cozido** (`ovo-de-galinha-cozido`) ← POF 7803301 Ovo de galinha, preparo 2
  - direta: Unidade 45 g (pág. 362)
  - semelhante (outro preparo, não usada): Colher de sopa 15 g; Colher de arroz/servir 45 g
- **Ovo de galinha frito** (`ovo-de-galinha-frito`) ← POF 7803301 Ovo de galinha, preparo 5
  - direta: Unidade 50 g (pág. 362)
  - semelhante (outro preparo, não usada): Fatia 10 g; Colher de sopa 15 g; Colher de arroz/servir 45 g; Escumadeira 45 g
- **Clara de ovo cozida** (`clara-de-ovo-cozida`): sem alimento correspondente na POF. Só gramas.
- **Leite integral** (`leite-integral`) ← POF 7900101 Leite de vaca integral, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de cafezinho 50 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 367)
- **Leite desnatado** (`leite-desnatado`) ← POF 7903601 Leite de vaca desnatado, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 384)
- **Iogurte natural** (`iogurte-natural`) ← POF 7901204 Iogurte natural, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Copo de requeijão 240 g (pág. 373)
- **Iogurte natural desnatado** (`iogurte-natural-desnatado`) ← POF 7901203 Iogurte desnatado, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g (pág. 373)
- **Queijo minas frescal** (`queijo-minas-frescal`) ⚠ genérico ← POF 7902001 Queijo de minas, preparo 99
  - direta: Fatia 45 g (pág. 378)
  - nota: POF "Queijo de minas": não separa o frescal.
- **Queijo muçarela** (`queijo-mucarela`) ← POF 7901801 Queijo muçarela, preparo 99
  - direta: Fatia 20 g (pág. 377)
- **Queijo prato** (`queijo-prato`) ← POF 7901701 Queijo prato, preparo 99
  - direta: Fatia 15 g (pág. 377)
- **Queijo parmesão** (`queijo-parmesao`): sem alimento correspondente na POF. Só gramas.
- **Ricota** (`ricota`) ← POF 7902201 Queijo ricota, preparo 99
  - direta: Fatia 35 g (pág. 379)
- **Requeijão cremoso** (`requeijao-cremoso`) ← POF 7902901 Requeijão, preparo 99
  - direta: Colher de sopa 30 g (pág. 381)
- **Pasta de amendoim integral** (`pasta-de-amendoim-integral`) ⚠ genérico ← POF 6901203 Pasta de amendoim, preparo 99
  - nenhuma medida direta nem semelhante: só gramas
  - nota: POF "Pasta de amendoim" (não separa a integral).
- **Amendoim torrado** (`amendoim-torrado`): sem alimento correspondente na POF. Só gramas.
- **Castanha-de-caju** (`castanha-de-caju`) ← POF 6600801 Castanha de caju, preparo 99
  - direta: Unidade 2,5 g; Punhado 40 g (pág. 100)
- **Castanha-do-pará** (`castanha-do-para`) ← POF 6600701 Castanha-do-pará, preparo 99
  - direta: Unidade 4 g (pág. 100)
- **Amêndoa torrada** (`amendoa-torrada`) ← POF 6600501 Amêndoa, preparo 99
  - direta: Unidade 1 g (pág. 100)
- **Noz** (`noz`) ← POF 6601501 Noz, preparo 99
  - direta: Unidade 5 g (pág. 101)
- **Linhaça** (`linhaca`) ← POF 6302001 Semente de linhaça, preparo 99
  - direta: Colher de sopa 13 g; Colher de sobremesa 3 g (pág. 41)
- **Azeite de oliva extra virgem** (`azeite-de-oliva-extra-virgem`) ← POF 8400101 Azeite de oliva, preparo 99
  - nenhuma medida direta nem semelhante: só gramas
- **Óleo de soja** (`oleo-de-soja`) ← POF 8400301 Óleo de soja, preparo 99
  - direta: Colher de sopa 8 g; Colher de chá 2 g (pág. 474)
- **Manteiga** (`manteiga`) ← POF 7901501 Manteiga com ou sem sal, preparo 99
  - direta: Colher de sopa 32 g; Colher de sobremesa 23 g; Colher de chá 8 g (pág. 375)
- **Açúcar refinado** (`acucar-refinado`) ← POF 6906602 Açúcar, preparo 99
  - direta: Colher de sopa 24 g; Colher de sobremesa 16 g; Colher de chá 5 g (pág. 205)
  - nota: POF "Açúcar".
- **Açúcar mascavo** (`acucar-mascavo`) ← POF 6900304 Açúcar mascavo, preparo 99
  - direta: Colher de sopa 19 g (pág. 176)
- **Mel** (`mel`) ← POF 6901602 Mel, preparo 99
  - direta: Colher de sopa 15 g; Colher de sobremesa 9 g; Colher de chá 3 g (pág. 188)
- **Café coado** (`cafe-coado`) ← POF 8501302 Café, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de cafezinho 50 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 505)
- **Chá mate** (`cha-mate`): sem alimento correspondente na POF. Só gramas.
- **Chá preto** (`cha-preto`) ⚠ genérico ← POF 8206301 Chá (preto, camomila, erva cidreira, capim limão, etc.), preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de cafezinho 50 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 458)
  - nota: POF "Chá (preto, camomila, erva cidreira, capim limão, etc.)".
- **Chá de erva-doce** (`cha-de-erva-doce`) ⚠ genérico ← POF 8206301 Chá (preto, camomila, erva cidreira, capim limão, etc.), preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de cafezinho 50 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 458)
  - nota: POF "Chá (preto, camomila, erva cidreira, capim limão, etc.)".
- **Suco de laranja natural** (`suco-de-laranja-natural`) ← POF 8500407 Suco de laranja, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 488)
- **Água de coco** (`agua-de-coco`) ← POF 8202101 Água de coco, preparo 99
  - direta: Unidade 340 g; Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Copo de requeijão 240 g; Copo tulipa 290 g (pág. 448)
- **Refrigerante de cola** (`refrigerante-de-cola`) ← POF 8200101 Refrigerante de cola tradicional, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Xícara de chá 200 g; Caneca 300 g; Copo de cafezinho 50 g; Copo de requeijão 240 g; Copo tulipa 290 g (pág. 434)
- **Refrigerante de guaraná** (`refrigerante-de-guarana`) ← POF 8200301 Refrigerante de guaraná tradicional, preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Caneca 300 g; Copo de requeijão 240 g; Copo tulipa 290 g; Xícara de café 50 g (pág. 439)
- **Cerveja pilsen** (`cerveja-pilsen`) ← POF 8300101 Cerveja (com ou sem álcool), preparo 99
  - direta: Copo americano 150 g; Copo médio 240 g; Copo grande 300 g; Caneca 300 g; Copo de requeijão 240 g; Copo tulipa 290 g (pág. 464)