# Fase 2a: tabela dos 121 itens (grupo de troca, nutriente principal e medidas caseiras)

Aprovada em 2026-10-20 (com os três ajustes: filé semelhante só para tilápia, merluza e corvina; "bife 100 g"
só nos cinco cortes que se comem como bife; requeijão no grupo de gordura). O que está no código
(`BaseNutricional.cs` e os dois arquivos `BaseNutricionalClassificacao.cs` e `BaseNutricionalMedidas.cs`) é
exatamente isto. **97 itens têm medida caseira (313 medidas: 249 diretas, 61 por categoria e 3 semelhantes) e
24 ficam só em gramas.**

## Grupos de troca e nutriente principal (resumo)

| Grupo | Itens | Nutriente principal |
|---|---|---|
| CarnesEPeixes | bovinas, aves, suína, peixes (inclui atum) | Proteína |
| Ovos | ovo cozido, frito, clara | Proteína |
| LeiteEIogurte | leite integral e desnatado, iogurtes | Proteína |
| Queijos | minas frescal, muçarela, prato, parmesão, ricota | Proteína |
| Leguminosas | feijões carioca, preto e fradinho, lentilha | **Proteína** (decisão: o papel no prato) |
| Carboidratos | cereais, massas, pães, tubérculos, tapioca, aveia, farinha de mandioca | Carboidrato |
| Frutas | todas, menos o abacate (inclui açaí) | Carboidrato |
| Hortalicas | as 23 | Livre |
| OleaginosasESementes | castanhas, noz, amêndoa, amendoim, pasta de amendoim, linhaça e o **abacate** | Gordura |
| OleosEGorduras | azeite, óleo de soja, manteiga e o **requeijão** (pasta de pão) | Gordura |
| Adocantes | açúcares e mel | Carboidrato |
| CafesEChas | café, chás | Livre |
| SucosERefrigerantes | suco de laranja, água de coco, refrigerantes | Carboidrato |
| BebidasAlcoolicas | cerveja | Livre |

Julgamentos (todos comentados no código):
- Feijão como proteína (decisão): o macro que mais pesa é o carboidrato (~70% das kcal), mas o papel no prato é a proteína.
- Ovo e pão de queijo: mais de 50% das kcal em gordura, mas entram como proteína e como pão.
- Requeijão no grupo de gordura, com a manteiga (decisão): é pasta de pão.
- Abacate no grupo de gordura (é fruta na TACO); açaí em Frutas como carboidrato (56% das kcal são gordura).
- Carboidratos é um grupo só (cereal, massa, pão, tubérculo): o cuscuz e o macarrão servem no prato e no café.
  Quem ordena os parecidos e filtra por refeição é a feature da troca.
- Um teste exige que o nutriente principal entregue ao menos 10% das kcal do item e que todo grupo tenha um nutriente só.

## Os 121 itens

| # | Item | Grupo de troca | Nutriente | Medidas (cat. = origem categoria) |
|---|---|---|---|---|
| 1 | Arroz branco cozido | Carboidratos | Carboidrato | colher de sopa 25 g; colher de servir 45 g; concha 100 g; escumadeira 85 g (+1) |
| 2 | Arroz integral cozido | Carboidratos | Carboidrato | colher de sopa 20 g; colher de servir 63 g; concha 117 g; escumadeira 59 g (+1) |
| 3 | Aveia em flocos | Carboidratos | Carboidrato | colher de sopa 15 g; colher de sobremesa 7 g; colher de chá 2 g |
| 4 | Macarrão cozido | Carboidratos | Carboidrato | colher de sopa 25 g; colher de servir 50 g; escumadeira 110 g; pegador 110 g (+3) |
| 5 | Cuscuz de milho cozido | Carboidratos | Carboidrato | pedaço 135 g |
| 6 | Milho verde em conserva | Carboidratos | Carboidrato | colher de sopa 24 g; colher de sobremesa 17 g |
| 7 | Tapioca | Carboidratos | Carboidrato | só gramas |
| 8 | Farinha de mandioca torrada | Carboidratos | Carboidrato | colher de sopa 16 g; colher de sobremesa 10 g; colher de chá 5 g; colher de servir 33 g (+7) |
| 9 | Pão francês | Carboidratos | Carboidrato | unidade 50 g |
| 10 | Pão de forma integral | Carboidratos | Carboidrato | fatia 25 g (cat.) |
| 11 | Pão de queijo assado | Carboidratos | Carboidrato | unidade 20 g; unidade pequena 10 g |
| 12 | Feijão cozido | Leguminosas | Proteina | colher de sopa 17 g (cat.); colher de servir 35 g (cat.); concha 140 g (cat.) |
| 13 | Feijão preto cozido | Leguminosas | Proteina | colher de sopa 17 g (cat.); colher de servir 35 g (cat.); concha 140 g (cat.) |
| 14 | Feijão fradinho cozido | Leguminosas | Proteina | colher de sopa 17 g (cat.); colher de servir 35 g (cat.); concha 140 g (cat.) |
| 15 | Lentilha cozida | Leguminosas | Proteina | colher de sopa 18 g; colher de servir 32 g; concha 160 g |
| 16 | Batata inglesa cozida | Carboidratos | Carboidrato | unidade 140 g; unidade pequena 70 g; colher de sopa 30 g; colher de servir 60 g (+1) |
| 17 | Batata doce cozida | Carboidratos | Carboidrato | fatia 70 g; colher de sopa 42 g |
| 18 | Mandioca cozida | Carboidratos | Carboidrato | pedaço 100 g (cat.); colher de sopa 30 g (cat.); colher de servir 60 g (cat.) |
| 19 | Mandioquinha cozida | Carboidratos | Carboidrato | unidade 80 g; colher de sopa 35 g |
| 20 | Cará cozido | Carboidratos | Carboidrato | só gramas |
| 21 | Alface crua | Hortalicas | Livre | unidade 200 g; unidade pequena 130 g; colher de sopa 8 g; folha 10 g (+4) |
| 22 | Rúcula crua | Hortalicas | Livre | ramo 6 g |
| 23 | Agrião cru | Hortalicas | Livre | colher de sopa 7 g; garfada 30 g; ramo 5 g; pires 10 g (+1) |
| 24 | Couve crua | Hortalicas | Livre | folha 20 g |
| 25 | Tomate cru | Hortalicas | Livre | unidade 100 g; unidade pequena 50 g; fatia 15 g; colher de sopa 30 g |
| 26 | Pepino cru | Hortalicas | Livre | unidade 100 g; colher de sopa 18 g |
| 27 | Cenoura crua | Hortalicas | Livre | unidade 120 g; unidade pequena 55 g; colher de sopa 12 g |
| 28 | Cenoura cozida | Hortalicas | Livre | unidade 100 g; unidade pequena 46 g; fatia 5 g; escumadeira 48 g |
| 29 | Beterraba crua | Hortalicas | Livre | unidade 140 g; colher de sopa 16 g; prato de sobremesa 168 g |
| 30 | Beterraba cozida | Hortalicas | Livre | unidade 125 g; unidade pequena 75 g; fatia 12 g; colher de sopa 20 g (+3) |
| 31 | Brócolis cozido | Hortalicas | Livre | colher de sopa 10 g; pires 75 g |
| 32 | Couve-flor cozida | Hortalicas | Livre | unidade pequena 30 g; colher de sopa 25 g; ramo 60 g; prato de sobremesa 100 g |
| 33 | Abobrinha cozida | Hortalicas | Livre | unidade 335 g; fatia 61 g; pedaço 17 g; colher de sopa 30 g (+3) |
| 34 | Abobrinha refogada | Hortalicas | Livre | unidade 335 g; pedaço 17 g; rodela 18 g |
| 35 | Chuchu cozido | Hortalicas | Livre | unidade 230 g; unidade pequena 145 g; pedaço 30 g; colher de sopa 20 g (+2) |
| 36 | Couve refogada | Hortalicas | Livre | colher de sopa 20 g; folha 25 g |
| 37 | Espinafre refogado | Hortalicas | Livre | só gramas |
| 38 | Repolho cru | Hortalicas | Livre | colher de sopa 10 g; colher de servir 25 g; escumadeira 25 g; folha 26 g (+1) |
| 39 | Repolho roxo cru | Hortalicas | Livre | colher de sopa 10 g (cat.); colher de servir 25 g (cat.); escumadeira 25 g (cat.); folha 26 g (cat.) (+1) |
| 40 | Acelga crua | Hortalicas | Livre | colher de sopa 6 g; colher de servir 10 g; folha 10 g |
| 41 | Cebola crua | Hortalicas | Livre | unidade 70 g; unidade pequena 30 g; fatia 6 g; colher de sopa 10 g |
| 42 | Abóbora cozida | Hortalicas | Livre | pedaço 50 g; colher de sopa 36 g; escumadeira 100 g |
| 43 | Berinjela cozida | Hortalicas | Livre | só gramas |
| 44 | Banana prata | Frutas | Carboidrato | unidade 75 g; pedaço 16 g; prato raso 235 g |
| 45 | Banana nanica | Frutas | Carboidrato | pedaço 16 g (cat.); prato raso 235 g (cat.) |
| 46 | Maçã fuji | Frutas | Carboidrato | unidade 150 g (cat.); unidade pequena 90 g (cat.) |
| 47 | Pera williams | Frutas | Carboidrato | unidade 130 g (cat.) |
| 48 | Laranja pera | Frutas | Carboidrato | unidade 180 g; unidade pequena 90 g |
| 49 | Tangerina | Frutas | Carboidrato | unidade 135 g; unidade pequena 100 g |
| 50 | Kiwi | Frutas | Carboidrato | unidade 94 g; rodela 11 g |
| 51 | Mamão papaia | Frutas | Carboidrato | unidade 310 g (cat.); unidade pequena 270 g (cat.); fatia 170 g (cat.); colher de sopa 40 g (cat.) (+1) |
| 52 | Mamão formosa | Frutas | Carboidrato | fatia 170 g (cat.) |
| 53 | Abacaxi | Frutas | Carboidrato | unidade 750 g; unidade pequena 480 g; fatia 75 g |
| 54 | Melancia | Frutas | Carboidrato | fatia 200 g; pedaço 150 g |
| 55 | Melão | Frutas | Carboidrato | unidade 660 g; fatia 90 g |
| 56 | Morango | Frutas | Carboidrato | unidade 12 g; unidade pequena 7 g |
| 57 | Uva itália | Frutas | Carboidrato | unidade 8 g (cat.); unidade pequena 4 g (cat.) |
| 58 | Manga palmer | Frutas | Carboidrato | só gramas |
| 59 | Abacate | OleaginosasESementes | Gordura | unidade 430 g; unidade pequena 370 g; colher de sopa 45 g |
| 60 | Goiaba vermelha | Frutas | Carboidrato | unidade 170 g (cat.); unidade pequena 50 g (cat.) |
| 61 | Açaí sem açúcar | Frutas | Carboidrato | concha 101 g; tigela 202 g; copo médio 273 g; copo grande 326 g |
| 62 | Patinho bovino grelhado | CarnesEPeixes | Proteina | bife 100 g (cat.) |
| 63 | Patinho bovino moído refogado | CarnesEPeixes | Proteina | colher de sopa 25 g (cat.); colher de servir 60 g (cat.) |
| 64 | Carne moída de acém cozida | CarnesEPeixes | Proteina | colher de sopa 25 g (cat.); colher de servir 60 g (cat.) |
| 65 | Acém bovino cozido | CarnesEPeixes | Proteina | só gramas |
| 66 | Alcatra bovina grelhada | CarnesEPeixes | Proteina | bife 100 g (cat.) |
| 67 | Contrafilé bovino grelhado | CarnesEPeixes | Proteina | bife 100 g (cat.) |
| 68 | Coxão mole bovino cozido | CarnesEPeixes | Proteina | só gramas |
| 69 | Lagarto bovino cozido | CarnesEPeixes | Proteina | só gramas |
| 70 | Músculo bovino cozido | CarnesEPeixes | Proteina | só gramas |
| 71 | Maminha bovina grelhada | CarnesEPeixes | Proteina | bife 100 g (cat.) |
| 72 | Filé mignon grelhado | CarnesEPeixes | Proteina | bife 100 g (cat.) |
| 73 | Peito de frango sem pele grelhado | CarnesEPeixes | Proteina | só gramas |
| 74 | Peito de frango sem pele cozido | CarnesEPeixes | Proteina | só gramas |
| 75 | Coxa de frango sem pele cozida | CarnesEPeixes | Proteina | só gramas |
| 76 | Sobrecoxa de frango sem pele assada | CarnesEPeixes | Proteina | sobrecoxa 65 g (cat.) |
| 77 | Frango sem pele assado | CarnesEPeixes | Proteina | sobrecoxa 65 g; peito 180 g; asa 40 g |
| 78 | Lombo suíno assado | CarnesEPeixes | Proteina | só gramas |
| 79 | Bisteca suína grelhada | CarnesEPeixes | Proteina | só gramas |
| 80 | Pernil suíno assado | CarnesEPeixes | Proteina | só gramas |
| 81 | Filé de tilápia grelhado | CarnesEPeixes | Proteina | filé 120 g (sem.) |
| 82 | Salmão grelhado | CarnesEPeixes | Proteina | só gramas |
| 83 | Sardinha assada | CarnesEPeixes | Proteina | só gramas |
| 84 | Atum em conserva em água | CarnesEPeixes | Proteina | colher de sopa 16 g; colher de sobremesa 10 g |
| 85 | Merluza assada | CarnesEPeixes | Proteina | filé 120 g (sem.) |
| 86 | Corvina assada | CarnesEPeixes | Proteina | filé 120 g (sem.) |
| 87 | Ovo de galinha cozido | Ovos | Proteina | unidade 45 g |
| 88 | Ovo de galinha frito | Ovos | Proteina | unidade 50 g |
| 89 | Clara de ovo cozida | Ovos | Proteina | só gramas |
| 90 | Leite integral | LeiteEIogurte | Proteina | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+5) |
| 91 | Leite desnatado | LeiteEIogurte | Proteina | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+4) |
| 92 | Iogurte natural | LeiteEIogurte | Proteina | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g |
| 93 | Iogurte natural desnatado | LeiteEIogurte | Proteina | copo americano 150 g; copo médio 240 g |
| 94 | Queijo minas frescal | Queijos | Proteina | só gramas |
| 95 | Queijo muçarela | Queijos | Proteina | fatia 20 g |
| 96 | Queijo prato | Queijos | Proteina | fatia 15 g |
| 97 | Queijo parmesão | Queijos | Proteina | só gramas |
| 98 | Ricota | Queijos | Proteina | fatia 35 g |
| 99 | Requeijão cremoso | OleosEGorduras | Gordura | colher de sopa 30 g |
| 100 | Pasta de amendoim integral | OleaginosasESementes | Gordura | só gramas |
| 101 | Amendoim torrado | OleaginosasESementes | Gordura | só gramas |
| 102 | Castanha-de-caju | OleaginosasESementes | Gordura | unidade 2,5 g; punhado 40 g |
| 103 | Castanha-do-pará | OleaginosasESementes | Gordura | unidade 4 g |
| 104 | Amêndoa torrada | OleaginosasESementes | Gordura | unidade 1 g |
| 105 | Noz | OleaginosasESementes | Gordura | unidade 5 g |
| 106 | Linhaça | OleaginosasESementes | Gordura | colher de sopa 13 g; colher de sobremesa 3 g |
| 107 | Azeite de oliva extra virgem | OleosEGorduras | Gordura | só gramas |
| 108 | Óleo de soja | OleosEGorduras | Gordura | colher de sopa 8 g; colher de chá 2 g |
| 109 | Manteiga | OleosEGorduras | Gordura | colher de sopa 32 g; colher de sobremesa 23 g; colher de chá 8 g |
| 110 | Açúcar refinado | Adocantes | Carboidrato | colher de sopa 24 g; colher de sobremesa 16 g; colher de chá 5 g |
| 111 | Açúcar mascavo | Adocantes | Carboidrato | colher de sopa 19 g |
| 112 | Mel | Adocantes | Carboidrato | colher de sopa 15 g; colher de sobremesa 9 g; colher de chá 3 g |
| 113 | Café coado | CafesEChas | Livre | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+5) |
| 114 | Chá mate | CafesEChas | Livre | só gramas |
| 115 | Chá preto | CafesEChas | Livre | copo americano 150 g (cat.); copo médio 240 g (cat.); copo grande 300 g (cat.); copo de requeijão 240 g (cat.) (+5) |
| 116 | Chá de erva-doce | CafesEChas | Livre | copo americano 150 g (cat.); copo médio 240 g (cat.); copo grande 300 g (cat.); copo de requeijão 240 g (cat.) (+5) |
| 117 | Suco de laranja natural | SucosERefrigerantes | Carboidrato | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+4) |
| 118 | Água de coco | SucosERefrigerantes | Carboidrato | unidade 340 g; copo americano 150 g; copo médio 240 g; copo grande 300 g (+3) |
| 119 | Refrigerante de cola | SucosERefrigerantes | Carboidrato | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+4) |
| 120 | Refrigerante de guaraná | SucosERefrigerantes | Carboidrato | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+3) |
| 121 | Cerveja pilsen | BebidasAlcoolicas | Livre | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g (+2) |

## Medidas com origem "categoria" ou "semelhante"

Origem "categoria" = a POF só tem o alimento por categoria, ou descreve a fonte com outro nome do mesmo
alimento. Origem "semelhante" = a POF mediu outro preparo: só o filé de 120 g ("Peixe cozido - filé médio")
da tilápia, da merluza e da corvina. Cada uma está marcada `OrigemMedida.Categoria` ou
`OrigemMedida.Semelhante` no código.

| Item | Origem | Medidas | Por quê |
|---|---|---|---|
| Pão de forma integral | categoria | fatia 25 g | mantida da fase 1; a fonte é um pão de forma de aveia, a POF "Pão integral" usa o mesmo valor |
| Feijão cozido | categoria | colher de sopa 17 g; colher de servir 35 g; concha 140 g | POF só tem "Feijão (preto, mulatinho, roxo, rosinha, etc.)": não separa o carioca. |
| Feijão preto cozido | categoria | colher de sopa 17 g; colher de servir 35 g; concha 140 g | a POF mede "feijão branco cozido" para todos os feijões |
| Feijão fradinho cozido | categoria | colher de sopa 17 g; colher de servir 35 g; concha 140 g | POF "Feijão de corda" (fradinho = feijão-de-corda, nome regional). |
| Mandioca cozida | categoria | pedaço 100 g; colher de sopa 30 g; colher de servir 60 g | a fonte descreve "Aipim cozido" (mesmo alimento com outro nome) |
| Repolho roxo cru | categoria | colher de sopa 10 g; colher de servir 25 g; escumadeira 25 g; folha 26 g; pires 100 g | POF "Repolho" não separa a cor. |
| Banana nanica | categoria | pedaço 16 g; prato raso 235 g | POF junta todas as bananas; nanica = d´água. |
| Maçã fuji | categoria | unidade 150 g; unidade pequena 90 g | POF "Maçã" não separa a variedade. |
| Pera williams | categoria | unidade 130 g | POF "Pera" não separa a variedade. |
| Mamão papaia | categoria | unidade 310 g; unidade pequena 270 g; fatia 170 g; colher de sopa 40 g; colher de servir 70 g | POF "Mamão" não separa papaia e formosa. |
| Mamão formosa | categoria | fatia 170 g | POF "Mamão" não separa papaia e formosa. |
| Uva itália | categoria | unidade 8 g; unidade pequena 4 g | POF "Uva" não separa a variedade. |
| Goiaba vermelha | categoria | unidade 170 g; unidade pequena 50 g | POF "Goiaba" não separa a cor. |
| Patinho bovino grelhado | categoria | bife 100 g | a POF mede "bife bovino" para todos os cortes |
| Patinho bovino moído refogado | categoria | colher de sopa 25 g; colher de servir 60 g | POF "Carne moída" (não separa o corte), refogada. |
| Carne moída de acém cozida | categoria | colher de sopa 25 g; colher de servir 60 g | POF "Carne moída" (não separa o corte), cozida. |
| Alcatra bovina grelhada | categoria | bife 100 g | a POF mede "bife bovino" para todos os cortes |
| Contrafilé bovino grelhado | categoria | bife 100 g | a POF mede "bife bovino" para todos os cortes |
| Maminha bovina grelhada | categoria | bife 100 g | a POF mede "bife bovino" para todos os cortes |
| Filé mignon grelhado | categoria | bife 100 g | a POF mede "bife bovino" para todos os cortes |
| Sobrecoxa de frango sem pele assada | categoria | sobrecoxa 65 g | POF "Frango em pedaços" (medidas coxa e sobrecoxa). |
| Filé de tilápia grelhado | semelhante | filé 120 g | a POF mede o filé de peixe cozido (categoria de peixe), usado para o grelhado/assado |
| Merluza assada | semelhante | filé 120 g | a POF mede o filé de peixe cozido (categoria de peixe), usado para o grelhado/assado |
| Corvina assada | semelhante | filé 120 g | a POF mede o filé de peixe cozido (categoria de peixe), usado para o grelhado/assado |
| Chá preto | categoria | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g; copo tulipa 290 g; copo de cafezinho 50 g; xícara de chá 200 g; xícara de café 50 g; caneca 300 g | POF "Chá (preto, camomila, erva cidreira, capim limão, etc.)". |
| Chá de erva-doce | categoria | copo americano 150 g; copo médio 240 g; copo grande 300 g; copo de requeijão 240 g; copo tulipa 290 g; copo de cafezinho 50 g; xícara de chá 200 g; xícara de café 50 g; caneca 300 g | POF "Chá (preto, camomila, erva cidreira, capim limão, etc.)". |

## O que ficou de fora e por quê

- **Só gramas por regra (decisão de 2026-10-20):** salmão (se come em posta) e sardinha (inteira), porque a
  POF só tem "peixe de mar" genérico; queijo minas frescal (a POF mede o minas padrão, curado); músculo e
  acém bovinos (não se comem como bife); e o frango sem medida direta (peito grelhado e cozido, coxa).
- **Em espera, a conferir a linha no PDF (valores bem fora do usual, extração conferida):**
  - batata-doce, *unidade* 355 g (pág. 51 do PDF; o usual é ~130 a 250 g). Ficam a fatia 70 g e a colher de sopa 42 g.
  - lombo suíno, *fatia* 190 g (pág. 255; a própria POF dá 95 g para "1/2 fatia", e o usual é ~90 g). O lombo fica só em gramas.
- **Valores estranhos que mantive** (a extração bate com o PDF e a ordem de grandeza é plausível): beterraba
  "prato de sobremesa" 168 g, abacaxi unidade 750 g, castanha-de-caju unidade 2,5 g, amêndoa unidade 1 g.
- **Todos os 24 itens só em gramas:** Tapioca; Cará cozido; Espinafre refogado; Berinjela cozida; Manga palmer; Acém bovino cozido; Coxão mole bovino cozido; Lagarto bovino cozido; Músculo bovino cozido; Peito de frango sem pele grelhado; Peito de frango sem pele cozido; Coxa de frango sem pele cozida; Lombo suíno assado; Bisteca suína grelhada; Pernil suíno assado; Salmão grelhado; Sardinha assada; Clara de ovo cozida; Queijo minas frescal; Queijo parmesão; Pasta de amendoim integral; Amendoim torrado; Azeite de oliva extra virgem; Chá mate.
