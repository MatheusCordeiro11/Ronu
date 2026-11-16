"""Recorta a fonte do site para os caracteres que ele usa.

A fonte sai do próprio site (frontend/fontes/) em vez do Google Fonts: assim o
navegador de quem abre o Ronu não manda o IP para o Google só para baixar fonte
(LGPD base, etapa 6), e a CSP de produção libera só `font-src 'self'`.

Desde o redesign de nov/2026 o site usa uma família só, a Fustat (variável,
pesos 400 a 800). Ela é recortada para:

  - o latim das faixas "latin" e "latin-ext" do Google Fonts, que cobre o
    português e nomes digitados com acentos de outras línguas;
  - todo caractere fora dessas faixas que aparece nos .html, .js e .css de
    frontend/ e que a Fustat tenha (setas, sinais).

A Fustat não tem kanji: o que sobrar de kanji no frontend aparece na fonte de
reserva do sistema, e o script avisa quais são. Ao acrescentar um símbolo novo
no frontend, rode o script de novo.

O original (OFL 1.1) vem de https://github.com/google/fonts e não fica no
repositório:
  ofl/fustat/Fustat[wght].ttf
  ofl/fustat/OFL.txt

Saída em WOFF2, que precisa do módulo brotli além do fontTools:
  pip install fonttools brotli

Uso (da raiz do repositório):
  python docs/fontes-tipograficas/recortar.py <pasta-dos-originais>
A pasta tem a subpasta fustat/, com o .ttf e o OFL.txt.
"""

import io
import shutil
import sys
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

RAIZ = Path(__file__).resolve().parents[2]
FRONTEND = RAIZ / 'frontend'
SAIDA = FRONTEND / 'fontes'

# Faixas "latin" e "latin-ext" do Google Fonts.
FAIXAS_LATIM = [
    (0x0000, 0x00FF), (0x0131, 0x0131), (0x0152, 0x0153), (0x02BB, 0x02BC),
    (0x02C6, 0x02C6), (0x02DA, 0x02DA), (0x02DC, 0x02DC), (0x0304, 0x0304),
    (0x0308, 0x0308), (0x0329, 0x0329), (0x2000, 0x206F), (0x20AC, 0x20AC),
    (0x2122, 0x2122), (0x2191, 0x2191), (0x2193, 0x2193), (0x2212, 0x2212),
    (0x2215, 0x2215), (0xFEFF, 0xFEFF), (0xFFFD, 0xFFFD),
    (0x0100, 0x02BA), (0x02BD, 0x02C5), (0x02C7, 0x02CC), (0x02CE, 0x02D7),
    (0x02DD, 0x02FF), (0x1D00, 0x1DBF), (0x1E00, 0x1E9F), (0x1EF2, 0x1EFF),
    (0x2020, 0x2020), (0x20A0, 0x20AB), (0x20AD, 0x20C0), (0x2113, 0x2113),
    (0x2C60, 0x2C7F), (0xA720, 0xA7FF),
]


def latim():
    return {c for ini, fim in FAIXAS_LATIM for c in range(ini, fim + 1)}


def usados_no_frontend():
    """Caracteres fora do latim que aparecem no código do frontend."""
    base = latim()
    achados = set()
    for arquivo in FRONTEND.rglob('*'):
        if arquivo.suffix not in ('.html', '.js', '.css') or SAIDA in arquivo.parents:
            continue
        for ch in arquivo.read_text(encoding='utf-8'):
            c = ord(ch)
            if c not in base and c >= 0x20:
                achados.add(c)
    return achados


def recortar(fonte, unicodes, destino):
    opcoes = subset.Options()
    opcoes.flavor = 'woff2'
    opcoes.desubroutinize = True
    opcoes.name_IDs = ['*']  # mantém copyright e licença no arquivo
    opcoes.notdef_outline = True
    sub = subset.Subsetter(opcoes)
    sub.populate(unicodes=unicodes)
    sub.subset(fonte)
    fonte.flavor = 'woff2'
    fonte.save(destino)
    return destino.stat().st_size


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    originais = Path(sys.argv[1])
    SAIDA.mkdir(exist_ok=True)

    extras = usados_no_frontend()
    cjk = {c for c in extras if c >= 0x2E80}
    print(f'{len(extras)} caracteres fora do latim no frontend, {len(cjk)} deles CJK:',
          ''.join(sorted(chr(c) for c in extras)))

    unicodes = latim() | extras
    trabalhos = [
        # Variável: só os pesos que o CSS usa (400 a 800).
        ('fustat/Fustat[wght].ttf', 'fustat-400-800.woff2', {'wght': (400, 800)}),
    ]
    for origem, nome, eixos in trabalhos:
        fonte = TTFont(originais / origem)
        if eixos:
            # Salva e relê: recortar direto a instância em memória quebra
            # (tabelas lidas sob demanda ainda apontam para glifos antigos).
            buffer = io.BytesIO()
            instancer.instantiateVariableFont(fonte, eixos).save(buffer)
            buffer.seek(0)
            fonte = TTFont(buffer)
        cmap = fonte.getBestCmap()
        faltam = sorted(c for c in extras if c not in cmap)
        tamanho = recortar(fonte, unicodes, SAIDA / nome)
        aviso = f' (sem glifo para: {"".join(chr(c) for c in faltam)})' if faltam else ''
        print(f'{nome}: {tamanho / 1024:.1f} KB{aviso}')

    shutil.copyfile(originais / 'fustat/OFL.txt', SAIDA / 'OFL-fustat.txt')


if __name__ == '__main__':
    main()
