"""Gera os dados da fase 2a da base nutricional: classificação (grupo de troca e nutriente principal)
e medidas caseiras com origem, a partir da revisão da POF (medidas_por_item.csv) e das decisões de
2026-10-20. Saídas: BaseNutricionalClassificacao.cs, BaseNutricionalMedidas.cs, tabela-2a.md,
categoria.md (a lista do que entrou por categoria)."""
import csv
import json
import re
import sys
import unicodedata

sys.path.insert(0, '.')
from mapeamento import MAPA
from selecao import n

itens = json.load(open('itens_base.json', encoding='utf-8'))
por_id = {i['id']: i for i in itens}

# ---------------------------------------------------------------- decisões de 2026-10-20
# 1. Medidas semelhantes (outro preparo): frango, nenhuma. Peixe (decisão final de 2026-10-20): o filé de
#    120 g ("Peixe cozido - filé médio") entra, com origem semelhante, só para tilápia, merluza e corvina, que
#    se comem em filé. Salmão (posta) e sardinha (inteira) ficam só em gramas.
FILE_SEMELHANTE = {'file-de-tilapia-grelhado', 'merluza-assada', 'corvina-assada'}
# 2. Categoria que mistura alimentos diferentes, ou item que não se come assim: só gramas.
MISTURA = {
    'queijo-minas-frescal': 'POF "Queijo minas padrão" (curado) não é o frescal',
    'salmao-grelhado': 'o salmão se come em posta, e a POF só tem "peixe de mar" genérico',
    'sardinha-assada': 'a sardinha se come inteira, e a POF só tem "peixe de mar" genérico',
    'musculo-bovino-cozido': 'o músculo não se come como bife',
    'acem-bovino-cozido': 'o acém não se come como bife',
}
# 3. Valores fora do esperado, em espera da conferência da linha no PDF.
EM_ESPERA = {
    ('batata-doce-cozida', 'Unidade'): 'unidade média de 355 g (p. 51), bem acima do usual (~130 a 250 g)',
    ('lombo-suino-assado', 'Fatia'): 'fatia de 190 g (p. 255), o dobro do usual (~90 g, e a POF dá 95 g para "1/2 fatia")',
}
# Medidas que a fase 1 já tinha como "peso por unidade" (EhUnidade): mesmos valores, agora com origem.
UNIDADE = {'pao-frances': 'Unidade', 'pao-de-forma-integral': 'Fatia', 'pao-de-queijo-assado': 'Unidade',
           'banana-prata': 'Unidade', 'maca-fuji': 'Unidade', 'pera-williams': 'Unidade', 'laranja-pera': 'Unidade',
           'tangerina': 'Unidade', 'kiwi': 'Unidade', 'ovo-de-galinha-cozido': 'Unidade', 'ovo-de-galinha-frito': 'Unidade'}
# Pão de forma integral: a fonte é "pão de forma de aveia - unidade" (fase 1), medida da POF "Pão integral".
EXTRA = [dict(item='pao-de-forma-integral', medida='Fatia', gramas='25', codigo_alimento_pof='8001401',
              descricao_na_fonte='Pão de forma de aveia - unidade', pagina_pdf='395', origem='categoria',
              motivo='mantida da fase 1; a fonte é um pão de forma de aveia, a POF "Pão integral" usa o mesmo valor')]
# Origem "categoria" também quando a fonte é outro membro do mesmo grupo (feijões) ou o genérico do corte (bife bovino).
CATEGORIA_FORCADA = {
    'feijao-preto-cozido': 'a POF mede "feijão branco cozido" para todos os feijões',
}

# ---------------------------------------------------------------- classificação
G = dict(carnes='CarnesEPeixes', ovos='Ovos', leite='LeiteEIogurte', queijos='Queijos', leg='Leguminosas',
         carbo='Carboidratos', frutas='Frutas', hort='Hortalicas', oleag='OleaginosasESementes',
         oleos='OleosEGorduras', adoc='Adocantes', cafe='CafesEChas', suco='SucosERefrigerantes',
         alc='BebidasAlcoolicas')
PADRAO = {  # categoria da base -> (grupo, nutriente)
    'CarneBovina': ('carnes', 'Proteina'), 'Aves': ('carnes', 'Proteina'), 'CarneSuina': ('carnes', 'Proteina'),
    'Peixes': ('carnes', 'Proteina'), 'Ovos': ('ovos', 'Proteina'), 'Leguminosas': ('leg', 'Proteina'),
    'CereaisEMassas': ('carbo', 'Carboidrato'), 'Paes': ('carbo', 'Carboidrato'),
    'TuberculosERaizes': ('carbo', 'Carboidrato'), 'Frutas': ('frutas', 'Carboidrato'),
    'Hortalicas': ('hort', 'Livre'), 'OleaginosasESementes': ('oleag', 'Gordura'),
    'OleosEGorduras': ('oleos', 'Gordura'), 'AcucaresEMel': ('adoc', 'Carboidrato'),
    'BebidasAlcoolicas': ('alc', 'Livre'),
}
EXCECOES = {
    'abacate': ('oleag', 'Gordura', 'fruta na TACO, mas no prato é fonte de gordura'),
    'acai-sem-acucar': ('frutas', 'Carboidrato', 'polpa com gordura (56% das kcal), mas no prato entra como fruta'),
    'iogurte-natural': ('leite', 'Proteina', ''), 'iogurte-natural-desnatado': ('leite', 'Proteina', ''),
    'leite-integral': ('leite', 'Proteina', ''), 'leite-desnatado': ('leite', 'Proteina', ''),
    'queijo-minas-frescal': ('queijos', 'Proteina', ''), 'queijo-mucarela': ('queijos', 'Proteina', ''),
    'queijo-prato': ('queijos', 'Proteina', ''), 'queijo-parmesao': ('queijos', 'Proteina', ''),
    'ricota': ('queijos', 'Proteina', ''), 'requeijao-cremoso': ('oleos', 'Gordura', 'usado como pasta no pão, junto com a manteiga'),
    'cafe-coado': ('cafe', 'Livre', ''), 'cha-mate': ('cafe', 'Livre', ''), 'cha-preto': ('cafe', 'Livre', ''),
    'cha-de-erva-doce': ('cafe', 'Livre', ''),
    'suco-de-laranja-natural': ('suco', 'Carboidrato', ''), 'agua-de-coco': ('suco', 'Carboidrato', ''),
    'refrigerante-de-cola': ('suco', 'Carboidrato', ''), 'refrigerante-de-guarana': ('suco', 'Carboidrato', ''),
    'feijao-cozido': ('leg', 'Proteina', 'decisão: o papel do feijão no prato é a proteína (e 25 a 29% das kcal)'),
    'ovo-de-galinha-cozido': ('ovos', 'Proteina', 'a gordura passa de 50% das kcal, mas o ovo entra como proteína'),
    'ovo-de-galinha-frito': ('ovos', 'Proteina', 'idem'),
    'pao-de-queijo-assado': ('carbo', 'Carboidrato', 'a gordura passa de 50% das kcal, mas entra como pão'),
}

classe = {}
for it in itens:
    if it['id'] in EXCECOES:
        g, nut, obs = EXCECOES[it['id']]
    else:
        g, nut = PADRAO[it['cat']]
        obs = ''
    classe[it['id']] = (G[g], nut, obs)

# ---------------------------------------------------------------- medidas
NOMES = {
    'Unidade': ('unidade', 'unidades'), 'Unidade pequena': ('unidade pequena', 'unidades pequenas'),
    'Fatia': ('fatia', 'fatias'), 'Pedaço': ('pedaço', 'pedaços'), 'Bife': ('bife', 'bifes'), 'Filé': ('filé', 'filés'),
    'Coxa': ('coxa', 'coxas'), 'Sobrecoxa': ('sobrecoxa', 'sobrecoxas'), 'Peito': ('peito', 'peitos'),
    'Asa': ('asa', 'asas'), 'Posta': ('posta', 'postas'),
    'Colher de sopa': ('colher de sopa', 'colheres de sopa'),
    'Colher de sobremesa': ('colher de sobremesa', 'colheres de sobremesa'),
    'Colher de chá': ('colher de chá', 'colheres de chá'),
    'Colher de arroz/servir': ('colher de servir', 'colheres de servir'),
    'Concha': ('concha', 'conchas'), 'Escumadeira': ('escumadeira', 'escumadeiras'), 'Pegador': ('pegador', 'pegadores'),
    'Garfada': ('garfada', 'garfadas'), 'Punhado': ('punhado', 'punhados'), 'Folha': ('folha', 'folhas'),
    'Ramo': ('ramo', 'ramos'), 'Rodela': ('rodela', 'rodelas'), 'Gomo': ('gomo', 'gomos'), 'Cacho': ('cacho', 'cachos'),
    'Pires': ('pires', 'pires'), 'Prato de sobremesa': ('prato de sobremesa', 'pratos de sobremesa'),
    'Prato raso': ('prato raso', 'pratos rasos'), 'Prato fundo': ('prato fundo', 'pratos fundos'),
    'Tigela': ('tigela', 'tigelas'), 'Pote': ('pote', 'potes'), 'Copo americano': ('copo americano', 'copos americanos'),
    'Copo médio': ('copo médio', 'copos médios'), 'Copo grande': ('copo grande', 'copos grandes'),
    'Copo de requeijão': ('copo de requeijão', 'copos de requeijão'), 'Copo tulipa': ('copo tulipa', 'copos tulipa'),
    'Copo de cafezinho': ('copo de cafezinho', 'copos de cafezinho'),
    'Xícara de chá': ('xícara de chá', 'xícaras de chá'), 'Xícara de café': ('xícara de café', 'xícaras de café'),
    'Caneca': ('caneca', 'canecas'),
}
ORDEM = list(NOMES)

STOP = {'cozido', 'cozida', 'crua', 'cru', 'grelhado', 'grelhada', 'assado', 'assada', 'refogado', 'refogada', 'frito',
        'frita', 'sem', 'pele', 'conserva', 'prata', 'media', 'pequeno', 'pequena', 'grande'}


def tokens(s):
    return {t for t in re.split(r'[^a-z]+', n(s)) if len(t) >= 3 and t not in STOP}


linhas = list(csv.DictReader(open('medidas_por_item.csv', encoding='utf-8')))
medidas = {}   # id -> lista de dicts
em_espera = []
for r in linhas:
    i = r['item']
    if i in FILE_SEMELHANTE:
        if not (r['classe'] == 'SEMELHANTE' and r['medida'] == 'Filé'):
            continue   # do peixe, só o filé
    elif r['classe'] == 'SEMELHANTE':
        continue
    if i in MISTURA:
        continue
    if r['medida'] == 'Unidade' and por_id[i]['cat'] in ('CarneBovina', 'Aves', 'CarneSuina', 'Peixes'):
        continue   # "unidade" de carne é o bife/filé, que já está listado com o nome certo
    if (i, r['medida']) in EM_ESPERA:
        em_espera.append((i, r['medida'], r['gramas'], EM_ESPERA[(i, r['medida'])]))
        continue
    tipo = MAPA[i][2]
    desc = r['descricao_na_fonte']
    alimento_fonte = n(desc).split(' - ')[0] if ' - ' in desc else ''
    if r['classe'] == 'SEMELHANTE':
        origem, motivo = 'semelhante', 'a POF mede o filé de peixe cozido (categoria de peixe), usado para o grelhado/assado'
    elif tipo == 'GENERICO':
        origem, motivo = 'categoria', MAPA[i][3]
    elif i in CATEGORIA_FORCADA:
        origem, motivo = 'categoria', CATEGORIA_FORCADA[i]
    elif por_id[i]['cat'] == 'CarneBovina':
        origem, motivo = 'categoria', 'a POF mede "bife bovino" para todos os cortes'
    elif alimento_fonte and not (tokens(alimento_fonte) & (tokens(r['alimento_pof']) | tokens(por_id[i]['nome']))):
        origem, motivo = 'categoria', f'a fonte descreve "{desc.split(" - ")[0]}" (mesmo alimento com outro nome)'
    else:
        origem, motivo = 'direta', ''
    medidas.setdefault(i, []).append(dict(item=i, medida=r['medida'], gramas=r['gramas'],
                                          codigo_alimento_pof=r['codigo_alimento_pof'], descricao_na_fonte=desc,
                                          pagina_pdf=r['pagina_pdf'], origem=origem, motivo=motivo))
for e in EXTRA:
    medidas.setdefault(e['item'], []).append(e)

for i in medidas:
    medidas[i].sort(key=lambda m: ORDEM.index(m['medida']))
    # sem duplicar a mesma medida
    vistos = set()
    medidas[i] = [m for m in medidas[i] if not (m['medida'] in vistos or vistos.add(m['medida']))]

def num(g):
    g = g.rstrip('0').rstrip('.') if '.' in g else g
    return g.replace('.', '.') + 'm'


def esc(s):
    return s.replace('\\', '\\\\').replace('"', '\\"')


# ---------------------------------------------------------------- C#
cs = ['// Medidas caseiras da fase 2a, geradas em 2026-10-20 a partir de docs/pof/ (tabela de revisão aprovada).', '// Para mudar uma medida: confira a linha na POF, edite aqui e em docs/pof/. O teste BaseNutricionalTests', '// confere cada medida contra docs/pof/medidas-pof-itens-mapeados.csv.', '',
      'namespace Ronu.Api.Data;', '', 'public static partial class BaseNutricional', '{',
      '    /// <summary>', '    /// Medidas caseiras por item (ver MedidaCaseira). Itens que não estão aqui só têm gramas.',
      '    /// </summary>', '    private static readonly IReadOnlyDictionary<string, MedidaCaseira[]> MedidasPorItem =',
      '        new Dictionary<string, MedidaCaseira[]>', '        {']
for it in itens:
    i = it['id']
    if i not in medidas:
        continue
    cs.append(f'            ["{i}"] =')
    cs.append('            [')
    for m in medidas[i]:
        nome, plural = NOMES[m['medida']]
        origem = {'direta': 'Direta', 'categoria': 'Categoria', 'semelhante': 'Semelhante'}[m['origem']]
        unidade = ', EhUnidade: true' if UNIDADE.get(i) == m['medida'] else ''
        cs.append(f'                new("{nome}", "{plural}", {num(m["gramas"])}, OrigemMedida.{origem}, "{m["codigo_alimento_pof"]}", {int(m["pagina_pdf"])}, "{esc(m["descricao_na_fonte"])}"{unidade}),')
    cs.append('            ],')
cs += ['        };', '}', '']
open('BaseNutricionalMedidas.cs', 'w', encoding='utf-8').write('\n'.join(cs))

cl = ['// Classificação da fase 2a (2026-10-20): grupo de troca e nutriente principal de cada item. Julgamento do', '// projeto, não valor de tabela; o raciocínio está nos comentários dos enums GrupoDeTroca e NutrientePrincipal.', '',
      'namespace Ronu.Api.Data;', '', 'public static partial class BaseNutricional', '{',
      '    private static readonly IReadOnlyDictionary<string, (GrupoDeTroca Grupo, NutrientePrincipal Nutriente)> Classificacao =',
      '        new Dictionary<string, (GrupoDeTroca, NutrientePrincipal)>', '        {']
for it in itens:
    g, nut, obs = classe[it['id']]
    cs_obs = f'  // {obs}' if obs else ''
    cl.append(f'            ["{it["id"]}"] = (GrupoDeTroca.{g}, NutrientePrincipal.{nut}),{cs_obs}')
cl += ['        };', '}', '']
open('BaseNutricionalClassificacao.cs', 'w', encoding='utf-8').write('\n'.join(cl))

# ---------------------------------------------------------------- tabelas para você
fm = lambda m: f"{NOMES[m['medida']][0]} {m['gramas'].replace('.', ',')} g" + (' (cat.)' if m['origem'] == 'categoria' else (' (sem.)' if m['origem'] == 'semelhante' else ''))
tab = ['| # | Item | Grupo de troca | Nutriente | Medidas (cat. = origem categoria) |', '|---|---|---|---|---|']
for k, it in enumerate(itens, 1):
    g, nut, obs = classe[it['id']]
    ms = medidas.get(it['id'], [])
    txt = '; '.join(fm(m) for m in ms[:4]) + (f' (+{len(ms) - 4})' if len(ms) > 4 else '') if ms else 'só gramas'
    tab.append(f"| {k} | {it['nome']} | {g} | {nut} | {txt} |")
open('tabela-2a.md', 'w', encoding='utf-8').write('\n'.join(tab))

cat = []
for it in itens:
    for m in medidas.get(it['id'], []):
        if m['origem'] != 'direta':
            cat.append((it['nome'], NOMES[m['medida']][0], m['gramas'], m['origem'], m['motivo'], m['pagina_pdf']))
por_item = {}
for nome_, med, g, o, mot, pg in cat:
    por_item.setdefault((nome_, o, mot), []).append(f"{med} {g.replace('.', ',')} g")
cmd = ['| Item | Origem | Medidas | Por quê |', '|---|---|---|---|']
for (nome_, o, mot), ms in por_item.items():
    cmd.append(f"| {nome_} | {o} | {'; '.join(ms)} | {mot} |")
open('categoria.md', 'w', encoding='utf-8').write('\n'.join(cmd))

tot = sum(len(v) for v in medidas.values())
so_gramas = [it['nome'] for it in itens if it['id'] not in medidas]
print('itens com medida:', len(medidas), '| só gramas:', len(so_gramas), '| medidas:', tot)
print('origens:', {o: sum(1 for v in medidas.values() for m in v if m['origem'] == o) for o in ('direta', 'categoria', 'semelhante')})
print('em espera:', em_espera)
print('itens por categoria:', len(por_item))
json.dump(dict(so_gramas=so_gramas, em_espera=em_espera), open('resumo_2a.json', 'w', encoding='utf-8'), ensure_ascii=False)
