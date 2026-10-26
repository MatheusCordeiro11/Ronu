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
osso, caroço), os pares crus do macarrão e da tilápia e o peso da clara de um ovo (seção abaixo).

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

## O que não vem de tabela

- **Itens derivados:** quando a fonte não tem o alimento, mas tem um equivalente próximo (ex.: patinho
  moído, a partir do patinho grelhado da TACO). A observação do item explica a derivação. A tapioca é a
  fécula de mandioca da TACO (124) hidratada na proporção da tapioca medida pela própria TACO (551, com a
  manteiga descontada): 100 g de tapioca pronta = 89,7 g de fécula + 10,3 g de água.
- **Porção máxima por refeição:** calibração do projeto.
- **Marcas de alérgenos** (lactose, glúten, amendoim, peixe, crustáceo, ovo): conservadoras, ou seja, na
  dúvida o item é marcado. Servem para tirar itens do cardápio de quem informou a restrição e **não
  substituem orientação de um médico ou nutricionista**.
