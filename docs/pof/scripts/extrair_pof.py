"""Extrai a Tabela de Medidas Referidas da POF 2008-2009 (IBGE) para CSV.

Entrada: texto gerado por `pdftotext -table -enc UTF-8 -f 35 -l 536 liv50000.pdf` (dados)
e `pdftotext -layout -enc UTF-8 -f 18 -l 30 liv50000.pdf` (quadros de códigos).
Saída: CSV com uma linha por (alimento, preparação, medida), exatamente como na tabela.
Nada é calculado nem inferido aqui: só se separam as colunas do PDF.
"""
import csv
import re
import sys
import unicodedata


def norm(s):
    s = unicodedata.normalize('NFKD', s)
    s = ''.join(c for c in s if not unicodedata.combining(c))
    return re.sub(r'\s+', ' ', s).strip().lower()


def quadro2(caminho):
    """Quadro 2: código -> nome da unidade de medida (106 itens)."""
    linhas = open(caminho, encoding='utf-8').read().split('\n')
    ini = next(i for i, l in enumerate(linhas) if 'Quadro 2 - Cadastro de unidades de medida' in l)
    fim = next(i for i, l in enumerate(linhas) if 'Quadro 3 - Fontes de refer' in l)
    medidas = {}
    for l in linhas[ini:fim]:
        for m in re.finditer(r'(?:^|\s{2,})(\d{1,3})\s{2,}(\S.*?)(?=\s{2,}\d{1,3}\s{2,}|\s*$)', l):
            medidas[int(m.group(1))] = re.sub(r'\s+', ' ', m.group(2)).strip()
    return medidas


PREPARACOES = {
    1: 'Cru(a)', 2: 'Cozido(a)', 3: 'Grelhado(a)/Brasa/Churrasco', 4: 'Assado(a)', 5: 'Frito(a)',
    6: 'Empanado(a) / à milanesa', 7: 'Refogado(a)', 8: 'Molho vermelho', 9: 'Molho branco',
    10: 'Ao alho e óleo', 11: 'Com manteiga/Óleo', 12: 'Ao vinagrete', 13: 'Ensopado', 14: 'Mingau',
    15: 'Sopa', 99: 'Não se aplica',
}


def parse_linha(tokens, medidas):
    """tokens = linha separada por espaços. Devolve dict ou None."""
    # 0: código do alimento (7 dígitos); depois o nome, até o código da preparação.
    cod = tokens[0]
    resto = tokens[1:]
    # Código da preparação: inteiro cujo nome (normalizado) abre o trecho seguinte.
    for i, t in enumerate(resto):
        if not t.isdigit() or int(t) not in PREPARACOES or i == 0:
            continue
        nome_prep = norm(PREPARACOES[int(t)])
        proximo = norm(' '.join(resto[i + 1:i + 5]))
        primeira = nome_prep.split(' ')[0]
        if proximo.startswith(primeira[:6]):
            alimento = ' '.join(resto[:i])
            cod_prep = int(t)
            depois = resto[i + 1:]
            break
    else:
        return None
    # Consome o nome da preparação (pode vir truncado/quebrado): avança até o código da medida.
    # Código da medida: inteiro cujo nome (Quadro 2) abre o trecho seguinte.
    for j, t in enumerate(depois):
        if not t.isdigit() or int(t) not in medidas or j == 0:
            continue
        nm = norm(medidas[int(t)])
        trecho = norm(' '.join(depois[j + 1:j + 1 + len(nm.split(' ')) + 1]))
        # nome quebrado com hífen no fim da linha ("Lata (não especi-"): compara só o começo
        if trecho.startswith(nm) or nm.startswith(norm(' '.join(depois[j + 1:j + 4])).rstrip('-')[:12]):
            cod_med = int(t)
            apos = depois[j + 1:]
            # consome as palavras do nome da medida
            n = len(medidas[cod_med].split())
            apos = apos[n:]
            break
    else:
        return None
    # Padrão (opcional): inteiro + nome da medida padrão, que pode quebrar em duas linhas
    # no PDF ("Colher de" / "arroz/servir"): o nome vem do Quadro 2, não do texto da linha.
    cod_pad = None
    if apos and apos[0].isdigit() and int(apos[0]) in medidas:
        cod_pad = int(apos[0])
        apos = apos[1:]
    # Quantidade (g) = primeiro número seguido do código da fonte (inteiro de 1 a 2 dígitos).
    for k in range(len(apos) - 1):
        if re.fullmatch(r'\d+(?:[.,]\d+)?', apos[k]) and re.fullmatch(r'\d{1,2}', apos[k + 1]):
            apos = apos[k:]
            break
    else:
        return None
    if not re.fullmatch(r'\d+(?:[.,]\d+)?', apos[0]) or not apos[1].isdigit():
        return None
    gramas = apos[0].replace('.', '').replace(',', '.') if ',' in apos[0] else apos[0]
    return {
        'codigo_alimento': cod, 'alimento': alimento,
        'codigo_preparacao': cod_prep, 'preparacao': PREPARACOES[cod_prep],
        'codigo_medida': cod_med, 'medida': medidas[cod_med],
        'codigo_medida_padrao': cod_pad if cod_pad is not None else '',
        'medida_padrao': medidas[cod_pad] if cod_pad is not None else '',
        'gramas': gramas, 'codigo_fonte': int(apos[1]), 'descricao_na_fonte': ' '.join(apos[2:]),
    }


def main(dados, quadros, saida):
    medidas = quadro2(quadros)
    paginas = open(dados, encoding='utf-8').read().split('\f')
    linhas_ok, falhas, total = [], [], 0
    for idx, pagina in enumerate(paginas):
        n_pagina = 35 + idx
        for linha in pagina.split('\n'):
            if not re.match(r'^\d{7}\s', linha):
                continue
            total += 1
            r = parse_linha(linha.split(), medidas)
            if r is None:
                falhas.append((n_pagina, linha.strip()))
            else:
                r['pagina_pdf'] = n_pagina
                linhas_ok.append(r)
    campos = ['codigo_alimento', 'alimento', 'codigo_preparacao', 'preparacao', 'codigo_medida', 'medida',
              'codigo_medida_padrao', 'medida_padrao', 'gramas', 'codigo_fonte', 'descricao_na_fonte', 'pagina_pdf']
    with open(saida, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=campos)
        w.writeheader()
        w.writerows(linhas_ok)
    print(f'medidas no Quadro 2: {len(medidas)}')
    print(f'linhas de dados: {total}; extraidas: {len(linhas_ok)}; falhas: {len(falhas)}')
    with open(saida.replace('.csv', '_falhas.txt'), 'w', encoding='utf-8') as f:
        for p, l in falhas:
            f.write(f'p{p}: {l}\n')


if __name__ == '__main__':
    main(*sys.argv[1:4])
