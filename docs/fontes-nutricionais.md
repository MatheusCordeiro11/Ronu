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

Usado só para o que a TACO não tem: tilápia, atum em conserva em água, pasta de amendoim, macarrão cozido
e leite fluido. O código de cada item é o `fdcId`.

## IBGE: peso por unidade

IBGE. **Pesquisa de Orçamentos Familiares 2008-2009: Tabela de Medidas Referidas para os Alimentos
Consumidos no Brasil.** Rio de Janeiro: IBGE, 2011. Disponível em:
https://biblioteca.ibge.gov.br/visualizacao/livros/liv50000.pdf

Licença: dados de publicações do IBGE podem ser usados por terceiros com a citação do IBGE como fonte.

Usado para o peso da "unidade média" dos itens contáveis (ovo, pão francês, fatia de pão de forma,
pão de queijo e frutas inteiras).

## O que não vem de tabela

- **Itens derivados:** quando a fonte não tem o alimento, mas tem um equivalente próximo (ex.: patinho
  moído, a partir do patinho grelhado da TACO). A observação do item explica a derivação. A tapioca é a
  fécula de mandioca da TACO (124) hidratada na proporção da tapioca medida pela própria TACO (551, com a
  manteiga descontada): 100 g de tapioca pronta = 89,7 g de fécula + 10,3 g de água.
- **Porção máxima por refeição:** calibração do projeto.
- **Marcas de alérgenos** (lactose, glúten, amendoim, peixe, crustáceo, ovo): conservadoras, ou seja, na
  dúvida o item é marcado. Servem para tirar itens do cardápio de quem informou a restrição e **não
  substituem orientação de um médico ou nutricionista**.
