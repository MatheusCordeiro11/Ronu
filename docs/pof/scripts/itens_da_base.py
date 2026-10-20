"""Lê os 121 itens de src/Ronu.Api/Data/BaseNutricional.cs (só leitura) e grava itens_base.json."""
import json
import re
import sys

caminho = sys.argv[1] if len(sys.argv) > 1 else '../../../src/Ronu.Api/Data/BaseNutricional.cs'
s = open(caminho, encoding='utf-8').read()
itens = []
for m in re.finditer(r'new\("([^"]+)", "([^"]+)", CategoriaAlimento\.(\w+), "(g|ml)", FonteNutricional\.(\w+), "([^"]*)", "([^"]*)", ([\d.]+)m, ([\d.]+)m, ([\d.]+)m, ([\d.]+)m, (true|false), ([\d.]+)m, ([^,\n]+)', s):
    itens.append(dict(id=m[1], nome=m[2], cat=m[3], med=m[4], fonte=m[5], cod=m[6], nomefonte=m[7],
                      kcal=m[8], ajust=m[12], max=m[13], alerg=m[14].strip()))
assert len(itens) == 121, len(itens)
json.dump(itens, open('itens_base.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(len(itens), 'itens')
