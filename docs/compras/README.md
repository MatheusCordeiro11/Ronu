# Lista de compras: fontes da conversão cozido → cru

A lista de compras soma a semana da dieta e converte cada alimento no que se compra (arroz cozido → arroz
cru, frango assado → frango inteiro, 525 g de banana → 7 bananas). A conversão está em
`src/Ronu.Api/Data/ConversaoCompras.cs` e é conferida pelo teste `ConversaoComprasTests` contra os arquivos
desta pasta.

## Os arquivos

| Arquivo | O que tem |
|---|---|
| `taco-tabela1.csv` | As 90 linhas da Tabela 1 da TACO usadas (o cozido de cada par, o cru e as da tapioca), com a página do PDF. Umidade, proteína, lipídeos e carboidrato como no PDF ("Tr" = traço). |
| `taco-trechos.csv` | Trechos de texto da TACO: as receitas do café, dos chás e do cuscuz (capítulo de procedimentos) e a lista de espécies que mostra que a corvina grande e a corvina do mar são a mesma espécie. |
| `usda-sr28.csv` | As 16 linhas do USDA SR Legacy usadas: os pares do macarrão e da tilápia, o descarte (casca, osso, caroço) e o peso da clara de um ovo grande. |
| `scripts/extrair_fontes_compras.py` | Gera os três CSVs a partir das fontes. Não calcula nada: só separa as colunas. |

## Fontes

- **TACO**, 4ª ed. rev. e ampl. (NEPA/UNICAMP, 2011), PDF oficial:
  https://www.cfn.org.br/wp-content/uploads/2017/03/taco_4_edicao_ampliada_e_revisada.pdf
  (744.346 bytes, SHA-256 `2002aec5615b5b1395aaa8fa675635bbb7f712c33f278af5e332f1cac8f108c8`).
- **USDA SR Legacy**, o SR28 ASCII do ARS, que tem o descarte (`Refuse` no `FOOD_DES`), que a API do FoodData
  Central não traz: https://www.ars.usda.gov/ARSUserFiles/80400535/DATA/SR/sr28/dnload/sr28asc.zip
  (6.231.932 bytes, SHA-256 `8308fd1d224ef1e5331093748007180da01a7ac713cbac6a1f5bc2a03e1ee70a`).
- **Correspondência NDB → fdcId** (a base guarda o fdcId): `sr_legacy_food.csv` do
  https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_sr_legacy_food_csv_2018-04.zip
  (SHA-256 `b80817294b8850530aaedf2e515c02593b1824f763a0ff356e5c2081643e6fd0`).

Nenhum dos três fica no repositório.

## Como gerar de novo

```bash
pdftotext -v   # poppler; o script chama o pdftotext -raw
python -I docs/compras/scripts/extrair_fontes_compras.py taco.pdf sr28/ sr_legacy_food.csv docs/compras/
```

O texto da TACO tem de sair do `pdftotext -raw`. O `-layout` embaralha as colunas em algumas páginas
(o valor de uma linha aparece na linha de outro alimento).

## O método

- **Par cru/cozido:** cru = cozido × (nutriente no cozido ÷ nutriente no cru), pelo nutriente que não se perde
  no preparo: carboidrato em grãos, massas e tubérculos; proteína em carnes, aves e peixes; matéria seca
  (100 − umidade) nas hortaliças, porque os nutrientes delas ficam abaixo de 5 g e variam demais entre amostras.
- **Descarte:** peso comprado = peso comestível ÷ (1 − descarte do USDA).
- **Contáveis:** divide pelo peso da unidade da POF 2008-2009, o mesmo da base. A contagem bate com a da dieta.
- **Receitas:** café (400 g de pó para 4 litros de água) e chás (20 sachês para 4 litros).

## Calibrações do projeto (não vêm de tabela)

- **Hortaliça cozida: fator no máximo 1.** No chuchu e na abóbora a TACO dá mais de 1, porque as amostras
  crua e cozida são de lotes diferentes. A lista não manda comprar menos cru do que se come.
- **Clara de ovo: 1 g cozida = 1 g crua.** A clara cozida na casca quase não perde peso. Dividir a proteína
  da TACO pela do USDA daria 1,23, e essa diferença vem das amostras.
- **Coxa, sobrecoxa e bisteca:** o peso é de carne sem osso (e sem pele, na coxa e na sobrecoxa), e a linha
  avisa ("compre um pouco mais"). O descarte do USDA para a coxa é inconsistente (66% num campo, 33% + 33% na descrição).

## Incertezas conhecidas

- **Peso da unidade da POF:** a POF não diz se é com ou sem casca. A lista conta as frutas pela mesma unidade
  da dieta, sem descarte.
- **Cuscuz:** a receita da TACO (2,5 kg de flocão para 2,5 litros de água) não fecha com a umidade do cuscuz
  pronto (71,1%). O fator pelo carboidrato (0,320) e pela matéria seca (0,328) concordam, mas é o item menos
  seguro da lista.
