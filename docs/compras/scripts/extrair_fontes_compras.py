"""Extrai das fontes oficiais as linhas que a lista de compras usa para converter cozido em cru.

Entradas (nenhuma fica no repositório; o README diz de onde baixar e o SHA-256 de cada uma):
  taco.pdf             TACO 4ª ed. (NEPA/UNICAMP, 2011), PDF oficial
  sr28/                SR28 ASCII do USDA (sr28asc.zip descompactado: FOOD_DES, NUT_DATA, WEIGHT)
  sr_legacy_food.csv   do FoodData_Central_sr_legacy_food_csv_2018-04.zip (correspondência fdc_id <-> NDB)

Saídas (em docs/compras/):
  taco-tabela1.csv     uma linha por código da TACO usado, como está na Tabela 1
  taco-trechos.csv     trechos de texto da TACO usados (receitas e lista de espécies)
  usda-sr28.csv        uma linha por item do SR Legacy usado (composição, descarte, peso da unidade)

Nada é calculado nem inferido aqui: só se separam as colunas da fonte. O texto da TACO sai do
`pdftotext -raw`, que mantém cada linha da tabela inteira. O `-layout` embaralha as colunas de
algumas páginas (linhas trocadas de lugar) e não serve.

Uso: python extrair_fontes_compras.py taco.pdf sr28/ sr_legacy_food.csv docs/compras/
"""
import csv
import os
import re
import subprocess
import sys

# Códigos da Tabela 1 da TACO usados na conversão (o item da base e o par cru, ou a derivação).
CODIGOS_TACO = [
    1, 2, 3, 4, 33, 64, 65, 70, 71, 86, 87, 88, 89, 91, 92, 95, 96, 97, 98, 100, 101, 102, 103,
    109, 110, 112, 113, 117, 118, 124, 129, 130, 140, 141, 261, 292, 293, 301, 302, 316, 317, 318,
    321, 326, 327, 328, 329, 345, 346, 351, 352, 357, 358, 363, 364, 367, 368, 369, 370, 371, 372,
    376, 377, 398, 399, 403, 405, 408, 409, 410, 413, 414, 427, 429, 432, 433, 435, 436, 486, 533,
    551, 561, 562, 563, 564, 567, 568, 577, 578,
]

# Trechos de texto da TACO: (código do alimento, título da receita ou None, começo exato da linha).
# Com título, a linha é procurada nas 12 linhas depois dele: os passos das receitas se repetem
# ("2. Esquentar 4 litros de água..." está no café e nos três chás).
TRECHOS_TACO = [
    ('471', 'Café, infusão 10%', '1. Pesar 400g de pó de café.'),
    ('471', 'Café, infusão 10%', '2. Esquentar 4 litros de água'),
    ('475', 'Chá, erva-doce, infusão 5%', '1. Pesar 20 gramas de chá; ou 20 saches de erva doce.'),
    ('475', 'Chá, erva-doce, infusão 5%', '2. Esquentar 4 litros de água'),
    ('476', 'Chá, mate, infusão 5%', '1. Pesar 20g ou 20 saches de chá mate.'),
    ('476', 'Chá, mate, infusão 5%', '2. Esquentar 4 litros de água'),
    ('477', 'Chá, preto, infusão 5%', '1. Pesar 20g ou 20 saches de chá preto.'),
    ('477', 'Chá, preto, infusão 5%', '2. Esquentar 4 litros de água'),
    ('533', 'Cuscuz, de milho, cozido com sal', '1. Pesar 2,5kg de farinha de milho floculada (amarela)'),
    ('292', None, 'Corvina do mar Micropogonias furnieri'),
    ('293', None, 'Corvina grande Micropogonias furnieri'),
]

# NDB do SR28 usados: pares cru/cozido do que vem do USDA, descarte (refuse) e peso da clara.
NDB_USDA = ['20121', '20120', '15262', '15261', '09040', '09226', '09266', '09326', '09184',
            '09176', '09037', '11352', '11507', '11134', '05011', '01124']

# A unidade usada de cada NDB, quando a conversão usa o peso de uma unidade (Msre_Desc no WEIGHT).
UNIDADE_USDA = {'01124': 'large'}


def paginas_taco(pdf):
    texto = subprocess.run(['pdftotext', '-raw', '-enc', 'UTF-8', pdf, '-'],
                           capture_output=True, check=True).stdout.decode('utf-8')
    return texto.split('\f')


def tabela1(paginas):
    """Primeira ocorrência de cada código com nome e 11 valores: a Tabela 1 vem antes das outras."""
    achados = {}
    for numero, pagina in enumerate(paginas, start=1):
        for linha in pagina.split('\n'):
            tokens = linha.split()
            if len(tokens) < 13 or not tokens[0].isdigit():
                continue
            codigo = int(tokens[0])
            if codigo not in CODIGOS_TACO or codigo in achados:
                continue
            valores = tokens[-11:]
            nome = ' '.join(tokens[1:-11])
            if not re.search(r'[A-Za-zÀ-ú]', nome) or not re.fullmatch(r'\d+,\d', valores[0]):
                continue
            # Colunas da Tabela 1: umidade, kcal, kJ, proteína, lipídeos, colesterol, carboidrato,
            # fibra, cinzas, cálcio, magnésio.
            achados[codigo] = {
                'codigo': codigo, 'nome_na_fonte': nome, 'pagina_pdf': numero,
                'umidade': valores[0], 'proteina': valores[3], 'lipideos': valores[4], 'carboidrato': valores[6],
            }
    faltam = sorted(set(CODIGOS_TACO) - set(achados))
    if faltam:
        sys.exit(f'Códigos da TACO não encontrados: {faltam}')
    return [achados[c] for c in sorted(achados)]


def trechos(paginas):
    linhas = [(n, l.strip()) for n, p in enumerate(paginas, start=1) for l in p.split('\n')]
    saida = []
    for codigo, titulo, inicio in TRECHOS_TACO:
        if titulo is None:
            candidatas = linhas
        else:
            ini = next((i for i, (_, l) in enumerate(linhas) if l == titulo), None)
            if ini is None:
                sys.exit(f'Título não encontrado: {titulo}')
            candidatas = linhas[ini + 1:ini + 13]
        achou = [(n, l) for n, l in candidatas if l.startswith(inicio)]
        if not achou:
            sys.exit(f'Trecho não encontrado: {inicio}')
        numero, linha = achou[0]
        saida.append({'codigo': codigo, 'titulo': titulo or '', 'pagina_pdf': numero, 'trecho': linha})
    return saida


def ler_sr28(pasta, arquivo):
    with open(os.path.join(pasta, arquivo), encoding='latin-1') as f:
        for linha in f:
            yield [c.strip('~') for c in linha.rstrip('\r\n').split('^')]


def usda(pasta, mapa_fdc):
    fdc = {}
    with open(mapa_fdc, encoding='utf-8') as f:
        for linha in csv.DictReader(f):
            fdc[linha['NDB_number'].zfill(5)] = linha['fdc_id']
    descricao = {c[0]: c for c in ler_sr28(pasta, 'FOOD_DES.txt') if c[0] in NDB_USDA}
    nutrientes = {}
    for c in ler_sr28(pasta, 'NUT_DATA.txt'):
        if c[0] in NDB_USDA and c[1] in ('255', '203', '205'):
            nutrientes[(c[0], c[1])] = c[2]
    pesos = {}
    for c in ler_sr28(pasta, 'WEIGHT.txt'):
        if c[0] in UNIDADE_USDA and c[3] == UNIDADE_USDA[c[0]] and c[2] == '1':
            pesos[c[0]] = c[4]
    saida = []
    for ndb in NDB_USDA:
        d = descricao[ndb]
        saida.append({
            'ndb': ndb, 'fdc_id': fdc[ndb], 'descricao': d[2],
            'agua': nutrientes.get((ndb, '255'), ''), 'proteina': nutrientes.get((ndb, '203'), ''),
            'carboidrato': nutrientes.get((ndb, '205'), ''),
            'descarte_pct': d[8], 'descarte_descricao': d[7],
            'unidade': UNIDADE_USDA.get(ndb, ''), 'gramas_unidade': pesos.get(ndb, ''),
        })
    return saida


def gravar(caminho, linhas):
    with open(caminho, 'w', encoding='utf-8', newline='') as f:
        escritor = csv.DictWriter(f, fieldnames=list(linhas[0]), lineterminator='\n')
        escritor.writeheader()
        escritor.writerows(linhas)


def main():
    pdf, pasta_sr28, mapa_fdc, saida = sys.argv[1:5]
    paginas = paginas_taco(pdf)
    gravar(os.path.join(saida, 'taco-tabela1.csv'), tabela1(paginas))
    gravar(os.path.join(saida, 'taco-trechos.csv'), trechos(paginas))
    gravar(os.path.join(saida, 'usda-sr28.csv'), usda(pasta_sr28, mapa_fdc))


if __name__ == '__main__':
    main()
