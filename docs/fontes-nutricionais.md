# Fontes dos dados nutricionais

A base nutricional própria do Ronu (`src/Ronu.Api/Data/BaseNutricional.cs`) reúne os alimentos que podem
entrar no cardápio e os valores de cada um por 100 g. Cada item registra a fonte, o código e o nome do
alimento na fonte.

## TACO (fonte principal)

NEPA/UNICAMP. **Tabela Brasileira de Composição de Alimentos (TACO).** 4ª ed. rev. e ampl. Campinas:
NEPA/UNICAMP, 2011. Disponível em: https://www.cfn.org.br/wp-content/uploads/2017/03/taco_4_edicao_ampliada_e_revisada.pdf

Licença, na página de créditos da publicação: "É permitida a reprodução parcial ou total desta obra, desde
que citada a fonte."

- Os valores vêm da Tabela 1 (composição centesimal), conferidos contra o **PDF oficial**. A transcrição
  em CSV do projeto [raulfdm/taco-api](https://github.com/raulfdm/taco-api) bateu com o PDF em todos os
  582 itens comparáveis (kcal, proteína, lipídeos e carboidrato), mas não é a fonte de referência.
- "NA" (não se aplica) conta como 0, como na proteína e no carboidrato dos óleos.
- A TACO não tem valores para o leite de vaca fluido, integral e desnatado (marcados com "*": "as análises
  estão sendo reavaliadas"). O leite vem do USDA.
- As preparações da TACO seguem as receitas do capítulo de procedimentos: arroz e feijão cozidos só com
  água, sem sal nem óleo; já os "refogados" incluem o óleo do preparo.

## USDA FoodData Central (complemento)

U.S. Department of Agriculture, Agricultural Research Service. **FoodData Central**, SR Legacy.
https://fdc.nal.usda.gov

Licença: domínio público (CC0 1.0); o USDA pede a citação do FoodData Central como fonte.

Na base, usado só para o que a TACO não tem: tilápia, atum em conserva em água, pasta de amendoim, macarrão
cozido e leite fluido. O código de cada item é o `fdcId`. A lista de compras usa também o descarte (casca,
osso, caroço), os pares crus do macarrão e da tilápia e o peso da clara de um ovo (seção abaixo). O selênio da
castanha-do-pará, que não está na base, vem dele também (seção "Selênio da castanha-do-pará").

## IBGE: medidas caseiras e peso por unidade

IBGE. **Pesquisa de Orçamentos Familiares 2008-2009: Tabela de Medidas Referidas para os Alimentos
Consumidos no Brasil.** Rio de Janeiro: IBGE, 2011. Disponível em:
https://biblioteca.ibge.gov.br/visualizacao/livros/liv50000.pdf

Licença: dados de publicações do IBGE podem ser usados por terceiros com a citação do IBGE como fonte.

Usado para as **medidas caseiras** (unidade, fatia, colher de sopa, colher de servir, concha, copo...)
dos itens, entre elas o peso da "unidade média" dos contáveis (ovo, pão francês, fatia de pão de forma,
pão de queijo e frutas inteiras). A POF preenche toda medida de todo alimento, emprestando valores de outro
utensílio ou de outro alimento, então só entra a medida que o IBGE mediu de fato (direta) ou que vale para o
mesmo alimento com outro nome (categoria). Cada medida guarda o código do alimento na POF, a página do PDF e
a descrição na fonte, e o teste a confere contra `docs/pof/medidas-pof-itens-mapeados.csv`. Método, regras e
a tabela de revisão estão em `docs/pof/`. As gramaturas da POF são a compilação do IBGE de outras tabelas
(Pinheiro et al. 2004, Guia Alimentar 2006, Philippi 2008, entre outras, citadas no Quadro 3 do PDF).

## Lista de compras: do prato ao mercado

A lista de compras (`Data/ConversaoCompras.cs`) converte cada item da base no que se compra: arroz cozido
em arroz cru, frango assado em frango inteiro, 525 g de banana em 7 bananas. As linhas das fontes, o script
de extração, os links e o SHA-256 dos arquivos usados estão em [`docs/compras/`](compras/README.md); o teste
`ConversaoComprasTests` confere o código contra elas e o lado cozido contra esta base.

- **Cozido → cru, pelo par da própria fonte:** cru = cozido × (nutriente no cozido ÷ nutriente no cru), pelo
  nutriente que não se perde no preparo: carboidrato em grãos, massas e tubérculos; proteína em carnes, aves
  e peixes; matéria seca (100 − umidade) nas hortaliças. Pares da Tabela 1 da TACO, conferidos no PDF oficial
  (o texto tem de sair do `pdftotext -raw`: o `-layout` embaralha as colunas em algumas páginas); macarrão e
  tilápia pelos pares do USDA SR Legacy. O fator nunca é digitado: sai das linhas da fonte.
- **Casca, osso e caroço:** o descarte ("refuse") do USDA SR Legacy, tirado do SR28 ASCII do ARS (a API do
  FoodData Central não traz esse campo): frango inteiro, batata, batata-doce, mandioca e as frutas vendidas
  por peso.
- **Contáveis:** o peso da unidade da POF 2008-2009 que a base já usa. A contagem bate com a da dieta.
- **Receitas da TACO:** café coado (400 g de pó para 4 litros de água) e chás (20 sachês para 4 litros).
  Tapioca → fécula pela derivação já descrita abaixo.
- **Pronto:** o que não tem fonte para converter (refogados, suco de laranja) aparece com a quantidade do
  prato pronto e a etiqueta "pronto". Nada de número inventado.

**Calibrações do projeto** (não vêm de tabela):

- **Hortaliça cozida: fator no máximo 1.** No chuchu e na abóbora a TACO dá mais de 1, porque as amostras
  crua e cozida são de lotes diferentes; a lista não manda comprar menos cru do que se come.
- **Clara de ovo: 1 g cozida = 1 g crua**, com 33 g de clara por ovo grande (USDA). A clara cozida na casca
  quase não perde peso; dividir a proteína da TACO pela do USDA daria 1,23, e essa diferença vem das amostras.
- **Coxa, sobrecoxa e bisteca:** o peso é de carne sem osso (e sem pele, no frango), e a própria linha avisa
  "compre um pouco mais". A TACO não tem o par com osso, e o descarte do USDA para a coxa é inconsistente
  (66% num campo, 33% + 33% na descrição).
- **Arredondamento sempre para cima:** unidade inteira nos contáveis; passos de 10 g ou 10 ml até 100 e de
  50 acima disso. Sobra um pouco, não falta.

**Incertezas conhecidas:**

- **Peso da unidade da POF com ou sem casca:** a POF não diz. A lista conta as frutas pela mesma unidade da
  dieta, sem descarte.
- **Cuscuz:** a receita da TACO (2,5 kg de flocão para 2,5 litros de água) não fecha com a umidade do cuscuz
  pronto (71,1%). O fator pelo carboidrato (0,320) e pela matéria seca (0,328) concordam, mas é o item menos
  seguro da lista.

### Embalagens

Desde 2026-11-16, a API da lista devolve também o que se compra em embalagens ("1 pacote de 1 kg", "2 latas
de 350 ml", "3 dúzias", "1 peça de 1 kg ou mais"), e a tela mostra a embalagem como número da linha, com
"você usa 650 g" embaixo do nome. **A conta continua em gramas** (ou ml, unidades, fatias): a embalagem é só a forma de
mostrar, calculada sobre a quantidade exata da semana, e o "já tenho" continua comparando pelos gramas.

**Os tamanhos são calibração do projeto com evidência, não valor de tabela.** Não há tabela pública do "tamanho
mais comum" de cada produto no Brasil. Cada tamanho, com a fonte, a amostra e o motivo, está em
[`docs/compras/embalagens.csv`](compras/embalagens.csv), uma linha por tamanho, e todo item de compra tem uma
linha com o seu grupo (embalagem, granel, contável, pronto ou peça). O teste `ConversaoComprasEmbalagensTests`
confere o código (`Data/ConversaoComprasEmbalagens.cs`) contra esse arquivo. Decisão de 2026-11-16.

- **Fonte principal: Open Food Facts**, base brasileira. Open Food Facts. *Open Food Facts database*.
  https://openfoodfacts.org, busca pela API `search.openfoodfacts.org` em **2026-11-16**, só produtos com código
  de barras da GS1 Brasil (789 e 790), filtrados pelo nome; o campo `quantity` normalizado em g ou ml. O script
  da consulta está em [`docs/compras/scripts/moda_embalagens_off.py`](compras/scripts/moda_embalagens_off.py).
  **Licença:** a base é da Open Food Facts, sob a Open Database License (ODbL) 1.0
  (https://opendatacommons.org/licenses/odbl/1-0/), e cada conteúdo sob a Database Contents License (DbCL) 1.0.
  O projeto guarda **só a contagem** dos tamanhos (quantas embalagens na amostra e quantas de cada tamanho),
  nunca as linhas dos produtos, e cita a fonte e a licença aqui, no CSV e no texto do "Ver detalhe".
- **Regra para entrar:** o item precisa de pelo menos **10 embalagens** com tamanho legível na amostra, e cada
  tamanho registrado tem pelo menos **15%** delas. Assim, "tamanho comum" quer dizer o mesmo para todos os itens.
  Quem não passa fica na quantidade de sempre, com o motivo no CSV.
- **Dúzia:** convenção de contagem, sem fonte. As bandejas de 20 e 30 ficaram de fora: o Open Food Facts traz o
  peso dos ovos, e a contagem aparece em poucos produtos.
- **Pão de forma:** o pacote é vendido em gramas (400 g e 500 g pelo Open Food Facts), e a lista conta fatias
  de 25 g (a mesma medida da POF, alimento 8001401, que a base usa): 16 e 20 fatias.
- **Frango inteiro: peça de 1 kg ou mais.** Calibração do projeto sem fonte pública (o peso de carcaça da
  Pesquisa Trimestral do Abate do IBGE é agregado e não foi verificado). Abaixo de 1 kg exato na semana, a API
  devolve a peça (`embalagem.tipo = "peca"`, "1 peça de 1 kg ou mais"), e a tela mostra a peça e, no "Ver
  detalhe", sugere trocar o alimento no painel; acima, fica em gramas (peça de peso variável).
- **Até 4 embalagens.** Acima disso a linha volta para a quantidade de sempre ("2,4 kg" é mais claro que
  "5 pacotes de 500 g"). O tamanho que passaria de 4 sai antes da escolha; entre os que sobram, fica o de menor
  total e, no empate, o de menos embalagens. Tamanhos diferentes não se combinam.

**Fora da v1, na quantidade de sempre** (motivo de cada um no CSV):

- vendidos também a granel ou no balcão, por peso (decisão de 2026-11-16): queijo muçarela e prato, amendoim,
  castanha-de-caju, castanha-do-pará, amêndoa e noz;
- peça de peso variável: queijo minas frescal e ricota;
- **peso drenado** (atum e milho em conserva): a lista conta o drenado, e o Open Food Facts mistura peso
  líquido e drenado no mesmo campo. Cadastrar o líquido mandaria comprar de menos;
- **azeite e óleo de soja:** a lista está em gramas e a garrafa em ml. Falta a densidade do arquivo WEIGHT do
  USDA SR28, que o script de `docs/compras/` ainda não extrai;
- **chás:** a lista conta sachês, e o Open Food Facts traz o peso da caixa;
- **pão de queijo:** a lista conta unidades pelo peso assado, e o pacote é vendido pelo peso da massa crua;
- **açaí:** a busca mistura polpa, sorvete e creme;
- amostra abaixo de 10 embalagens: feijão preto, feijão-fradinho, lentilha, flocão, fécula de mandioca, açúcar
  refinado, iogurte natural e chá preto.

**Revisão:** os tamanhos mudam com os anos (inclusive a redução de conteúdo dos pacotes). Rodar o script de novo
uma vez por ano e atualizar o CSV, a data da consulta (`DataConsultaOff`) e o código juntos.

## Cafeína (regra 7 do prompt)

A regra 7 só permite bebida com cafeína de manhã e no início da tarde, e o `ValidadorCafeina` confere em C# (jantar,
ceia ou a partir das 18:00). A TACO não informa cafeína; os valores vêm de duas fontes:

- U.S. Department of Agriculture, Agricultural Research Service. **FoodData Central**, SR Legacy (abril de 2018),
  nutriente 1057 (Caffeine), lido do arquivo `FoodData_Central_sr_legacy_food_csv_2018-04.zip` em 2026-11-02.
- ROCHA, P. L. A.; LIMA, A. L. C.; SAUNDERS, B.; REIS, C. E. G. **Development of a Caffeine Content Table for Foods,
  Drinks, Medications and Supplements Typically Consumed by the Brazilian Population (BraCaffT).** *Nutrients*, v.
  14, n. 20, 4417, 2022. https://doi.org/10.3390/nu14204417 (licença CC BY; valores da Tabela 1).

| Item da base | USDA (mg/100 ml) | BraCaffT (mg/100 ml) | BraCaffT por porção | Na regra 7 |
|---|---|---|---|---|
| Café coado | 40 (fdcId 171890, coado com água da torneira) | 30 (11 a 54) | 45 mg em 150 ml | sim |
| Chá preto | 20 (fdcId 173227) | 18 (12 a 32) | 36 mg em 200 ml | sim |
| Chá mate | não tem | 5 (3 a 6) na infusão; 26 (14 a 52) no chimarrão; 24 (17 a 36) no tereré | 10 mg em 200 ml | sim: a base não separa o preparo, e chimarrão e tereré passam de 20 |
| Refrigerante de cola | 9 (fdcId 174852) | 9 (8 a 10) | 32 mg em 350 ml | sim |
| Refrigerante de guaraná | não tem | 1 | 4 mg em 350 ml | não: menos que um café descafeinado (1 mg/100 ml na USDA, fdcId 171889) |
| Chá de erva-doce | 0 (fdcId 173232, chá de ervas exceto camomila) | não tem | | não; é o substituto na última tentativa |

Decisão de 2026-11-02. Os outros itens da base não têm cafeína (não há chocolate, cacau nem bebida energética).

## Selênio da castanha-do-pará

A base não guarda micronutrientes, e a **TACO não mede selênio** (os minerais da 4ª edição são cálcio, ferro,
magnésio, manganês, fósforo, sódio, potássio, cobre e zinco). Por isso, o limite é uma regra em gramas, só para a
castanha-do-pará, no C# (`Services/IA/LimiteCastanhaDoPara.cs`). Os macros dela continuam vindo da TACO 589.

| O quê | Valor | Fonte |
|---|---|---|
| Selênio da castanha-do-pará | 1.917 µg em 100 g (15 amostras, de 136 a 2.740 µg) | USDA FoodData Central, SR Legacy, "Nuts, brazilnuts, dried, unblanched", fdcId 170569 (NDB 12078) |
| Variação pela origem | mediana por estado de 2,07 mg/kg (MT) a 68,15 mg/kg (AM), ou seja, 207 a 6.815 µg em 100 g | Silva Junior, E. C. et al. "Natural variation of selenium in Brazil nuts and soils from the Amazon region". *Chemosphere* 188 (2017) 650–658, doi:10.1016/j.chemosphere.2017.08.158 |
| Limite superior tolerável (UL), todas as fontes | 400 µg/dia a partir dos 14 anos | IOM, *Dietary Reference Intakes for Vitamin C, Vitamin E, Selenium, and Carotenoids* (2000), cap. 7 |
| UL, todas as fontes | 255 µg/dia para adultos (inclusive gestantes e lactantes) | EFSA NDA Panel, *EFSA Journal* 2023;21(1):7704, doi:10.2903/j.efsa.2023.7704 |
| Peso da unidade | 4 g | POF/IBGE 6600701 (`docs/pof/medidas-por-item.csv`) |

Não há UL brasileiro; a ANVISA dá só o valor diário de referência para rotulagem (60 µg, IN 75/2020, Anexo II).

**Calibração do projeto (não é valor de tabela):** até **8 g por dia** (2 unidades, ~153 µg pela USDA) e em
**até 2 dias da semana**; nos outros dias ela sai, e uma refeição que fica sem nenhum item sai do dia. A porção
máxima da troca também é 8 g. Nenhum teto em gramas é seguro para castanha de qualquer origem (8 g do Amazonas dão
~545 µg), e a base não sabe a origem: o teto reduz a exposição, não a zera. Decisão de 2026-11-13, a partir das
respostas reais de `docs/medicoes` (g8b a g10d): a castanha-do-pará aparecia em 48% das dietas, com 10 a 30 g por dia
(mediana 15 g, ~288 µg pela USDA) e em até 3 dias da semana.

## O que não vem de tabela

- **Itens derivados:** quando a fonte não tem o alimento, mas tem um equivalente próximo (ex.: patinho
  moído, a partir do patinho grelhado da TACO). A observação do item explica a derivação. A tapioca é a
  fécula de mandioca da TACO (124) hidratada na proporção da tapioca medida pela própria TACO (551, com a
  manteiga descontada): 100 g de tapioca pronta = 89,7 g de fécula + 10,3 g de água.
- **Porção máxima por refeição:** calibração do projeto. Ela só limita o que o ajuste de quantidades sobe (itens
  ajustáveis) e a troca; o que o Gemini escreve num item não ajustável não tem teto, salvo a castanha-do-pará
  (seção do selênio).
- **Marcas de alérgenos** (lactose, glúten, amendoim, castanhas, soja, peixe, crustáceo, ovo): conservadoras, ou
  seja, na dúvida o item é marcado. Servem para tirar itens do cardápio de quem informou a restrição e **não
  substituem orientação de um médico ou nutricionista**. A observação de cada item marcado por precaução diz
  o porquê.

## Alergias declaradas (fase 2c)

A pessoa marca as alergias em 8 chips (Alimentação e onboarding). A lista de referência é o **Anexo III da
RDC nº 727, de 1º de julho de 2022, da ANVISA** ("Lista dos principais alimentos que causam alergias
alimentares"), em vigor desde 01/09/2022, que revogou a RDC 26/2015 com a mesma lista
([DOU](https://www.in.gov.br/en/web/dou/-/resolucao-rdc-n-727-de-1-de-julho-de-2022-413249279), acesso em
2026-11-13).

| Anexo III | Chip | Marca na base |
|---|---|---|
| 1. trigo, centeio, cevada, aveia e híbridos | Glúten | `Gluten` |
| 2. crustáceos | Crustáceos e frutos do mar | `Crustaceo` |
| 3. ovos | Ovo | `Ovo` |
| 4. peixes | Peixe | `Peixe` |
| 5. amendoim | Amendoim | `Amendoim` |
| 6. soja | Soja | `Soja` |
| 7. leites de mamíferos | Leite e derivados | `Lactose` |
| 8 a 17. amêndoa, avelã, castanha-de-caju, castanha-do-pará, macadâmia, nozes, pecã, pistache, pinoli, castanhas (*Castanea*) | Castanhas | `Castanhas` |

- O látex natural (item 18) não é alimento e fica fora.
- **Regra cruzada:** o chip Amendoim e o chip Castanhas tiram as duas marcas, porque os dois costumam ser
  processados juntos e quem reage a um pode reagir ao outro. A regra fica só em `RegrasAlergias`; as marcas
  continuam separadas na base para a regra poder mudar num lugar só, se um nutricionista orientar diferente.
- **Soja por precaução** (decisão de 2026-11-13): além do óleo de soja (o próprio alimento), a aveia em flocos
  ("pode conter soja" em rótulos comuns), o pão francês (melhoradores de farinha de padaria declaram "contém
  derivados de trigo e soja", e o pão de padaria não tem rótulo), o pão de forma integral e o pão de queijo
  (óleo ou grão de soja e "pode conter soja" nos rótulos de marcas comuns).
- **Castanhas:** castanha-de-caju, castanha-do-pará, amêndoa e noz passaram da marca `Amendoim` (provisória) para
  `Castanhas`.
- O que não está nos chips (gergelim, por exemplo) fica no "Prefiro evitar", que casa pelo nome e manda o texto
  à IA sem as palavras de saúde.
- **Revisão em curso:** a consulta pública da rotulagem aprovada pela ANVISA em 30/10/2025 (Voto nº
  311/2025, que absorveu a revisão de alergênicos) propõe incluir gergelim, trigo-sarraceno, aipo, tremoço e
  mostarda e excluir as castanhas do gênero *Castanea*. Até 2026-11-13 não havia norma nova publicada; a
  pendência está em `docs/lacunas-conhecidas.md`.
