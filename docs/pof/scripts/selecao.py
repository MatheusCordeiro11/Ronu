"""Classifica as linhas da POF de cada item da base: DIRETA, SEMELHANTE ou descartada.

A POF preenche toda medida de todo alimento, emprestando o valor de outro utensílio ou de outro
alimento quando a fonte não tem aquele (ex.: "Pegador" de arroz = 25 g, que é uma colher de sopa;
"Copo americano" de arroz = 100 g, que é um prato fundo raso). Serve para a pesquisa do IBGE, mas
não para dizer "1 concha" numa tela. Por isso:

  DIRETA     a descrição da fonte nomeia a MESMA medida (sem fração nem múltiplo), do MESMO alimento
             e do MESMO estado/preparo do item da base.
  SEMELHANTE mesma medida e mesmo alimento, mas a fonte mediu outro preparo (a própria POF
             documenta que usa "a preparação disponível e mais semelhante"): ex. filé de peixe
             medido cozido, usado para o grelhado. Fica listada para revisão, NÃO é usada por padrão.
  descartada medida emprestada de outro utensílio, de outro alimento, fração/múltiplo ou tamanho
             diferente do médio.
"""
import re
import unicodedata


def n(s):
    s = unicodedata.normalize('NFKD', s)
    return re.sub(r'\s+', ' ', ''.join(c for c in s if not unicodedata.combining(c))).strip().lower()


# código da medida -> expressão que o texto da descrição da fonte precisa ter (texto inteiro)
SUBSTANTIVO = {
    9: r'caneca', 12: r'colher de arroz', 14: r'colher de cha', 15: r'colher de sobremesa', 16: r'colher de sopa',
    17: r'concha', 28: r'escumadeira', 82: r'pegador', 35: r'garfada',
    32: r'fatia', 81: r'pedaco', 5: r'bife', 33: r'file', 25: r'coxa', 99: r'sobrecoxa', 83: r'peito',
    89: r'posta', 103: r'unidade', 104: r'(unidade|posta|ramo) pequen[ao]|pequen[ao]',
    92: r'prato fundo', 93: r'prato raso', 91: r'prato de sobremesa', 106: r'xicara de cha', 105: r'xicara de cafe',
    18: r'copo americano', 19: r'copo de cafezinho', 20: r'copo de requeijao', 21: r'copo grande', 22: r'copo medio',
    23: r'copo tulipa', 88: r'punhado', 26: r'cumbuca', 102: r'tigela', 34: r'folha', 96: r'rodela', 67: r'gomo',
    77: r'maco', 95: r'ramo', 8: r'cacho', 31: r'espiga', 90: r'pote', 85: r'pires', 1: r'asa', 24: r'costela',
    7: r'bola', 4: r'barra',
}

ESTADOS = {
    'cru': ['cozid', 'refogad', 'frit', 'assad', 'grelhad', 'ensopad', 'mexid'],
    'cozido': ['cru', 'refogad', 'frit', 'assad', 'grelhad', 'ensopad', 'mexid'],
    'refogado': ['cru', 'cozid', 'frit', 'assad', 'grelhad', 'ensopad', 'mexid'],
    'grelhado': ['cru', 'cozid', 'frit', 'assad', 'refogad', 'ensopad', 'mexid'],
    'assado': ['cru', 'cozid', 'frit', 'grelhad', 'refogad', 'ensopad', 'mexid'],
    'frito': ['cru', 'cozid', 'refogad', 'assad', 'grelhad', 'ensopad', 'mexid'],
}


def estado_do_item(item_id):
    for sufixo, chave in (('-cru', 'cru'), ('-crua', 'cru'), ('-cozido', 'cozido'), ('-cozida', 'cozido'),
                          ('-refogado', 'refogado'), ('-refogada', 'refogado'), ('-grelhado', 'grelhado'),
                          ('-grelhada', 'grelhado'), ('-assado', 'assado'), ('-assada', 'assado'), ('-frito', 'frito')):
        if item_id.endswith(sufixo):
            return chave
    return None


def classificar(item_id, codigo_medida, desc, palavras, rejeitar=None):
    """Devolve 'DIRETA', 'SEMELHANTE' ou None."""
    cod = int(codigo_medida)
    d = n(desc)
    partes = d.split(' - ', 1)
    alimento, cauda = (partes[0], partes[1]) if len(partes) == 2 else ('', d)
    pat = SUBSTANTIVO.get(cod)
    if pat is None or not re.search(pat, d):
        return None                      # medida emprestada de outro utensílio
    num = re.match(r'^(\d+(?:[.,]\d+)?)(?:/(\d+))?', cauda)
    if cauda.startswith(('meia', 'meio', 'metade')) or (num and (num.group(2) or float(num.group(1).replace(',', '.')) != 1)):
        return None                      # fração ("1/2 fatia") ou múltiplo ("2 conchas"); "1 fatia" vale
    if cod not in (104, 21) and re.search(r'\b(pequen[ao]|grande)\b', cauda):
        return None                      # a POF prefere o tamanho médio; só vale se a medida diz o tamanho
    if rejeitar and re.search(rejeitar, d):
        return None                      # outra variedade/alimento
    if alimento and not any(re.search(r'\b' + p, alimento) for p in palavras):
        return None                      # emprestada de outro alimento
    est = estado_do_item(item_id)
    if est and alimento and any(re.search(r, alimento) for r in ESTADOS[est]):
        return 'SEMELHANTE'              # outro preparo
    return 'DIRETA'
