# Medidas caseiras: extração da POF 2008-2009 (IBGE)

Pesquisa feita para a fase 2 da base nutricional (medida caseira por alimento, que a feature "trocar
alimento" mostra como "178 g · 1 concha e meia"). A fase 2a (2026-10-20) trouxe o resultado para o código:
`BaseNutricionalMedidas.cs`, conferido pelo teste `BaseNutricionalClassificacaoEMedidasTests` contra
`medidas-pof-itens-mapeados.csv`.

## Fonte

IBGE. **Pesquisa de Orçamentos Familiares 2008-2009: Tabela de Medidas Referidas para os Alimentos
Consumidos no Brasil.** Rio de Janeiro: IBGE, 2011. 545 páginas.
https://biblioteca.ibge.gov.br/visualizacao/livros/liv50000.pdf

- Licença: dados de publicações do IBGE podem ser usados por terceiros com a citação do IBGE como fonte
  (mesma fonte já usada para o peso por unidade, em `docs/fontes-nutricionais.md`).
- A TACO não tem medida caseira. A POF é a única fonte aberta com medida caseira em gramas para o
  consumo brasileiro. As gramaturas da POF são a compilação do IBGE de outras tabelas (Quadro 3 do
  próprio PDF: Pinheiro et al. 2004, Guia Alimentar 2006, Philippi 2008, Fisberg e Villar 2002, rótulos
  e outras), e cada linha traz o código da fonte e a descrição do alimento nela.
- PDF usado na extração: 3.032.908 bytes, SHA-256 `317ace59e53e25f3fc4913c5f552adf181bf001e6272edb5ae7a46804aa360b0`.
  O PDF não está no repositório.

## O que a tabela da POF é (e o que não é)

Uma linha por alimento, preparação, tipo de medida e quantidade em gramas, com o tipo de medida padrão e a
fonte de referência. A POF **preenche todas as medidas de todo alimento**, emprestando o valor de outro
utensílio, de outro preparo ou de outro alimento quando a fonte não tem aquele (a metodologia do próprio
PDF diz isso: "considerou-se a medida da preparação disponível e mais semelhante"). Exemplos reais:

- "Pegador" de arroz = 25 g, que é uma colher de sopa.
- "Copo americano" de arroz = 100 g, que é um prato fundo raso.
- "Colher de sopa" de cuscuz = 12 g, que é de farinha de milho seca, não de cuscuz cozido.
- "Copo médio" de mel = 240 g (o copo genérico de líquido), longe do peso real de 240 ml de mel.

Isso serve para a pesquisa do IBGE, mas não para dizer "1 concha" numa tela. Por isso as linhas foram
classificadas, e só entra no app o que o IBGE mediu de fato.

## Regras de classificação (`scripts/selecao.py`)

Para cada item da base, só entram linhas do alimento da POF mapeado em `scripts/mapeamento.py` (e do
preparo certo: cru, cozido, grelhado, assado, frito, refogado). Cada linha é:

- **DIRETA**: a descrição da fonte nomeia a *mesma* medida ("Arroz cozido - concha média cheia" para
  *Concha*), sem fração nem múltiplo ("1/2 colher de sopa", "2 conchas"), no tamanho médio, do *mesmo*
  alimento e do *mesmo* preparo do item. É a única classe usada por padrão.
- **Origem no código:** `Direta` quando a fonte é o próprio alimento; `Semelhante` quando a POF mediu outro preparo (só o filé de 120 g da tilápia, da merluza e da corvina); `Categoria` quando a POF só tem o
  alimento por categoria ou descreve a fonte com outro nome do mesmo alimento (decisão de 2026-10-20).
  Categorias que misturam alimentos diferentes (peixe de mar, queijo minas padrão para o frescal) ficam só em gramas.
  Medidas em espera (batata-doce unidade, lombo fatia) estão em `tabela-2a-121-itens.md`.
- **SEMELHANTE**: mesma medida e alimento, mas a fonte mediu outro preparo (ex.: "Peixe cozido - filé
  médio" para um filé grelhado). Fica listada para revisão e **só entra se você aceitar, item a item**.
- **Descartada**: medida emprestada de outro utensílio ou de outro alimento, fração, múltiplo, tamanho
  pequeno ou grande, copo/xícara/caneca genérico em alimento sólido ou em mel, variedade citada que não é
  a do item (ex.: "banana-prata" para banana nanica).

Os itens cujo alimento da POF é uma categoria mais ampla (peixe de água doce para a tilápia, "maçã" para
a fuji, "mamão" para papaia e formosa) estão marcados com ⚠ *genérico* na revisão.

## Como reproduzir

Numa pasta de trabalho com os scripts de `scripts/` e o PDF baixado (a extração precisa do `pdftotext`):

```
pdftotext -table  -enc UTF-8 -f 35 -l 536 liv50000.pdf dados_table.txt
pdftotext -layout -enc UTF-8 -f 18 -l 30  liv50000.pdf quadros.txt
python -I extrair_pof.py dados_table.txt quadros.txt medidas_pof.csv
python -I itens_da_base.py <caminho do BaseNutricional.cs>
python -I gerar_revisao.py
```

O `-table` do `pdftotext` (xpdf 4.00) separa as colunas da tabela; o modo `-layout` embaralha as linhas.

## Arquivos

- `tabela-2a-121-itens.md`: a tabela da fase 2a (grupo de troca, nutriente principal e medidas dos 121 itens, com o que entrou por categoria).
- `revisao-medidas.md`: item da base → linha da POF → gramas, para revisão.
- `medidas-por-item.csv`: as linhas DIRETA e SEMELHANTE de cada item, com alimento, preparo, descrição na
  fonte e página do PDF. É o arquivo que o código e o teste vão usar.
- `medidas-pof-itens-mapeados.csv`: **todas** as linhas da POF dos alimentos e preparos mapeados (1.288),
  como estão na tabela, para o teste conferir cada medida contra a fonte.
- `scripts/`: extração, mapeamento, classificação e geração da revisão.

## Conferência da extração

- 11.801 linhas de dados lidas, 0 falhas; 106 tipos de medida (Quadro 2), 1.119 alimentos.
- Verificações automáticas: toda medida "Grama" vale 1 g e toda "Quilo" vale 1000 g; nenhuma quantidade
  não numérica ou menor ou igual a zero.
- Os 11 pesos por unidade que a fase 1 já tinha (pão francês 50 g, pão de queijo 20 g, banana-prata 75 g,
  maçã 150 g, pera 130 g, laranja 180 g, tangerina 135 g, kiwi 94 g, ovo cozido 45 g, ovo frito 50 g e
  fatia de pão de forma 25 g) **conferem com a extração**: 10 como medida direta e a fatia do pão de forma
  como "unidade" do pão de forma de aveia.
