"""Monta docs/pof/tabela-2a-121-itens.md a partir de tabela-2a.md, categoria.md e resumo_2a.json
(saídas de gerar_2a.py)."""
import json
import sys

destino = sys.argv[1]
tab = open('tabela-2a.md', encoding='utf-8').read()
cat = open('categoria.md', encoding='utf-8').read()
r = json.load(open('resumo_2a.json', encoding='utf-8'))

cabecalho = """# Fase 2a: tabela dos 121 itens (grupo de troca, nutriente principal e medidas caseiras)

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

"""
secao = """

## Medidas com origem "categoria" ou "semelhante"

Origem "categoria" = a POF só tem o alimento por categoria, ou descreve a fonte com outro nome do mesmo
alimento. Origem "semelhante" = a POF mediu outro preparo: só o filé de 120 g ("Peixe cozido - filé médio")
da tilápia, da merluza e da corvina. Cada uma está marcada `OrigemMedida.Categoria` ou
`OrigemMedida.Semelhante` no código.

""" + cat + """

## O que ficou de fora e por quê

- **Só gramas por regra (decisão de 2026-10-20):** salmão (se come em posta) e sardinha (inteira), porque a
  POF só tem "peixe de mar" genérico; queijo minas frescal (a POF mede o minas padrão, curado); músculo e
  acém bovinos (não se comem como bife); e o frango sem medida direta (peito grelhado e cozido, coxa).
- **Em espera, a conferir a linha no PDF (valores bem fora do usual, extração conferida):**
  - batata-doce, *unidade* 355 g (pág. 51 do PDF; o usual é ~130 a 250 g). Ficam a fatia 70 g e a colher de sopa 42 g.
  - lombo suíno, *fatia* 190 g (pág. 255; a própria POF dá 95 g para "1/2 fatia", e o usual é ~90 g). O lombo fica só em gramas.
- **Valores estranhos que mantive** (a extração bate com o PDF e a ordem de grandeza é plausível): beterraba
  "prato de sobremesa" 168 g, abacaxi unidade 750 g, castanha-de-caju unidade 2,5 g, amêndoa unidade 1 g.
- **Todos os 24 itens só em gramas:** """ + '; '.join(r['so_gramas']) + """.
"""
open(destino, 'w', encoding='utf-8').write(cabecalho + tab + secao)
print('ok')
