"""Gera a tabela item da base -> linha da POF -> gramas para revisão.
Saídas: medidas_por_item.csv (todas as linhas DIRETA e SEMELHANTE) e revisao.md (texto de revisão)."""
import collections
import csv
import json
import sys

sys.path.insert(0, '.')
from mapeamento import MAPA, EXCLUIDAS
from palavras import PALAVRAS, REJEITAR
from selecao import classificar, n

itens = json.load(open('itens_base.json', encoding='utf-8'))
rows = list(csv.DictReader(open('medidas_pof.csv', encoding='utf-8')))
por = collections.defaultdict(list)
for r in rows:
    por[(r['codigo_alimento'], int(r['codigo_preparacao']))].append(r)

# Pesos por unidade que a base já tem (fase 1), para conferir contra a POF.
PESO_ATUAL = {'pao-frances': 50, 'pao-de-forma-integral': 25, 'pao-de-queijo-assado': 20, 'banana-prata': 75,
              'maca-fuji': 150, 'pera-williams': 130, 'laranja-pera': 180, 'tangerina': 135, 'kiwi': 94,
              'ovo-de-galinha-cozido': 45, 'ovo-de-galinha-frito': 50}

COPOS = {9, 10, 18, 19, 20, 21, 22, 23, 105, 106}
CAT_LIQUIDO = {'Bebidas', 'BebidasAlcoolicas', 'Laticinios'}
# Itens que são UM corte: as medidas dos outros cortes do mesmo frango não valem.
SO_MEDIDAS = {'sobrecoxa-de-frango-sem-pele-assada': {'Sobrecoxa'}, 'coxa-de-frango-sem-pele-cozida': {'Coxa'}}

ordem_medidas = ['Unidade', 'Unidade pequena', 'Fatia', 'Pedaço', 'Bife', 'Filé', 'Coxa', 'Sobrecoxa', 'Peito', 'Asa',
                 'Posta', 'Colher de sopa', 'Colher de sobremesa', 'Colher de chá', 'Colher de arroz/servir', 'Concha',
                 'Escumadeira', 'Pegador', 'Garfada', 'Punhado', 'Folha', 'Ramo', 'Rodela', 'Gomo', 'Cacho', 'Pires',
                 'Prato de sobremesa', 'Prato raso', 'Prato fundo', 'Tigela', 'Pote', 'Copo americano', 'Copo médio',
                 'Copo grande', 'Xícara de chá', 'Caneca']


def chave(m):
    return ordem_medidas.index(m) if m in ordem_medidas else 99


saida = []
md = []
resumo = collections.Counter()
for it in itens:
    m = MAPA[it['id']]
    if m is None:
        resumo['sem'] += 1
        md.append(f"- **{it['nome']}** (`{it['id']}`): sem alimento correspondente na POF. Só gramas.")
        continue
    cod, preps, tipo, nota = m
    rs = [r for p in preps for r in por[(cod, p)] if int(r['codigo_medida']) not in EXCLUIDAS]
    pal = PALAVRAS.get(it['id']) or [n(it['nome']).split()[0][:5]]
    rej = REJEITAR.get(it['id'])
    diretas, semelhantes = [], []
    for r in rs:
        c = classificar(it['id'], r['codigo_medida'], r['descricao_na_fonte'], pal, rej)
        # Copo, xícara e caneca sem alimento na descrição são medidas genéricas de líquido (a POF usa 240 g
        # para o copo médio de qualquer coisa): só valem para bebidas e laticínios líquidos.
        if c and int(r['codigo_medida']) in COPOS and ' - ' not in r['descricao_na_fonte'] and it['cat'] not in CAT_LIQUIDO:
            c = None
        if c and it['id'] in SO_MEDIDAS and r['medida'] not in SO_MEDIDAS[it['id']]:
            c = None
        if c == 'DIRETA':
            diretas.append(r)
        elif c == 'SEMELHANTE':
            semelhantes.append(r)
    nome_pof = (por[(cod, preps[0])] or [{'alimento': '?'}])[0]['alimento']
    for classe, lista in (('DIRETA', diretas), ('SEMELHANTE', semelhantes)):
        for r in lista:
            saida.append({'item': it['id'], 'tipo_correspondencia': tipo, 'classe': classe,
                          'medida': r['medida'], 'gramas': r['gramas'], 'codigo_alimento_pof': cod,
                          'alimento_pof': nome_pof, 'preparacao_pof': r['preparacao'],
                          'descricao_na_fonte': ' '.join(r['descricao_na_fonte'].split('Tabela de Medidas')[0].split('____')[0].split()),
                          'codigo_fonte': r['codigo_fonte'], 'pagina_pdf': r['pagina_pdf']})
    resumo['com_direta' if diretas else ('so_semelhante' if semelhantes else 'sem_medida')] += 1

    def fmt(lista):
        lista = sorted(lista, key=lambda r: chave(r['medida']))
        return '; '.join(f"{r['medida']} {r['gramas'].replace('.', ',')} g" for r in lista)
    marca = ' ⚠ genérico' if tipo == 'GENERICO' else ''
    linha = f"- **{it['nome']}** (`{it['id']}`){marca} ← POF {cod} {nome_pof}, preparo {','.join(str(p) for p in preps)}"
    if diretas:
        linha += f"\n  - direta: {fmt(diretas)} (pág. {min(int(r['pagina_pdf']) for r in diretas)})"
    if semelhantes:
        linha += f"\n  - semelhante (outro preparo, não usada): {fmt(semelhantes)}"
    if not diretas and not semelhantes:
        linha += "\n  - nenhuma medida direta nem semelhante: só gramas"
    if nota:
        linha += f"\n  - nota: {nota}"
    md.append(linha)

campos = ['item', 'tipo_correspondencia', 'classe', 'medida', 'gramas', 'codigo_alimento_pof', 'alimento_pof',
          'preparacao_pof', 'descricao_na_fonte', 'codigo_fonte', 'pagina_pdf']
with open('medidas_por_item.csv', 'w', encoding='utf-8', newline='') as f:
    w = csv.DictWriter(f, fieldnames=campos)
    w.writeheader()
    w.writerows(saida)
open('revisao.md', 'w', encoding='utf-8').write('\n'.join(md))

# Conferência dos 11 pesos por unidade que a base já tinha (fase 1)
print('--- pesos da fase 1 contra a POF ---')
for iid, g in PESO_ATUAL.items():
    achou = [s for s in saida if s['item'] == iid and s['classe'] == 'DIRETA' and s['medida'] in ('Unidade', 'Fatia') and float(s['gramas']) == g]
    print(f"{iid:28s} {g:4d} g -> {'confere (' + achou[0]['medida'] + ')' if achou else 'NÃO ENCONTRADO como direta'}")
print('--- resumo ---', dict(resumo), 'linhas diretas:', sum(1 for s in saida if s['classe'] == 'DIRETA'),
      'semelhantes:', sum(1 for s in saida if s['classe'] == 'SEMELHANTE'))
