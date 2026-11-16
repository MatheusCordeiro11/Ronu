"""Recorta um ícone 3D gerado no Figma (objeto sobre fundo branco liso) e grava em WebP com fundo transparente.

Uso:
    python tools/recorta-icone-3d.py <imagem-bruta.png> <saida.webp> [--tam 200] [--limiar 8]

Como funciona: o fundo é o branco neutro ligado à borda da imagem. O preenchimento para nas bordas do objeto
(pelo gradiente), então as partes brancas de dentro do objeto ficam (a carta do envelope, o papel da sacola).
Depois os buracos são preenchidos e a borda do recorte é suavizada.

Se o recorte comer uma parte clara do objeto, baixe o --limiar (6). Se sobrar halo branco em volta, suba (10).
Precisa de Pillow, NumPy e SciPy.
"""
import argparse
import pathlib

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage


def recortar(caminho, limiar):
    im = Image.open(caminho).convert('RGB')
    a = np.asarray(im).astype(np.int16)
    mn, mx = a.min(axis=2), a.max(axis=2)
    cinza = a.mean(axis=2)
    grad = np.hypot(ndimage.sobel(cinza, 0), ndimage.sobel(cinza, 1))
    neutro = (mn >= 200) & ((mx - mn) <= 8) & (grad < limiar)
    rotulos, _ = ndimage.label(neutro)
    borda = set(np.unique(np.concatenate([rotulos[0], rotulos[-1], rotulos[:, 0], rotulos[:, -1]]))) - {0}
    objeto = ndimage.binary_fill_holes(~np.isin(rotulos, list(borda)))
    objeto = ndimage.binary_opening(objeto, iterations=2)
    fundo = ndimage.binary_dilation(~objeto, iterations=1) & (mn >= 185)
    alfa = Image.fromarray(np.where(fundo, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
    rgba = Image.fromarray(np.dstack([np.asarray(im), np.asarray(alfa)]).astype(np.uint8), 'RGBA')
    caixa = rgba.getchannel('A').point(lambda v: 255 if v > 12 else 0).getbbox()
    return rgba.crop(caixa)


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument('entrada')
    p.add_argument('saida')
    p.add_argument('--tam', type=int, default=200, help='lado máximo em pixels (padrão 200)')
    p.add_argument('--limiar', type=float, default=8, help='gradiente que conta como borda (padrão 8)')
    a = p.parse_args()
    out = recortar(a.entrada, a.limiar)
    out.thumbnail((a.tam, a.tam), Image.LANCZOS)
    out.save(a.saida, 'WEBP', quality=84, method=6)
    print(f'{a.saida}: {out.size[0]}x{out.size[1]}, {pathlib.Path(a.saida).stat().st_size // 1024} KB')


if __name__ == '__main__':
    main()
