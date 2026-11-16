"""Conta os tamanhos de embalagem do Open Food Facts (produtos brasileiros) para a lista de compras.

Não calcula nada além da contagem: para cada item, faz a busca em https://search.openfoodfacts.org (filtro
countries_tags:"en:brazil", idioma pt), fica só com os códigos de barras da GS1 Brasil (789 e 790), filtra
pelo nome (regex de inclusão e de exclusão), normaliza o campo "quantity" (g, kg, ml, L) e conta. Mostra o
total de embalagens com tamanho legível (n_amostra) e os tamanhos mais comuns (n_tamanho).

Os produtos não são gravados: o docs/compras/embalagens.csv guarda só a contagem (ver a seção "Embalagens"
de docs/fontes-nutricionais.md, licença ODbL do Open Food Facts).

Uso:  python -I docs/compras/scripts/moda_embalagens_off.py
Consulta de referência: 2026-11-16. A base é colaborativa e muda: rodar de novo dá números um pouco diferentes.
"""
import json
import re
import time
import unicodedata
import urllib.parse
import urllib.request
from collections import Counter

USER_AGENT = "script-de-pesquisa/1.0"

# (chave, busca, regex que o nome tem de ter, regex que o nome não pode ter)
ITENS = [
    ("arroz-branco", "arroz", "arroz", "integral|parboil|arboreo|negro|vermelho|cateto|japon|basmati|sushi|7 graos|carne|frango|feijao|biscoito|bolacha|risoto|pronto|instantaneo|farinha|leite|bebida|cereal"),
    ("arroz-integral", "arroz integral", "arroz.*integral", "biscoito|bolacha|carne|frango|feijao|farinha|bebida|cereal|snack|tortilha|7 graos"),
    ("feijao-carioca", "feijão carioca", "feijao.*carioca", "carne|arroz|pronto|cozido|kaldinho|caldo"),
    ("feijao-preto", "feijão preto", "feijao.*preto", "carne|arroz|pronto|cozido|caldo|feijoada"),
    ("feijao-fradinho", "feijão fradinho", "fradinho|feijao.*corda", "pronto|cozido"),
    ("lentilha", "lentilha", "lentilha", "pronto|cozid|sopa|massa|snack|chips"),
    ("macarrao", "macarrão", "macarrao|espaguete|spaghetti|penne|parafuso|fusilli|talharim", "instantaneo|lamen|miojo|cup|copo|pronto|molho|nissin"),
    ("flocao", "flocão milho", "flocao|milho flocad|cuscuz|farinha de milho flocada", "pronto|massa para"),
    ("fecula-de-mandioca", "polvilho doce", "polvilho doce|fecula de mandioca|goma", "biscoito|tapioca pronta|azedo"),
    ("farinha-de-mandioca", "farinha de mandioca", "farinha de mandioca|farinha mandioca", "farofa|temperad"),
    ("aveia", "aveia em flocos", "aveia", "biscoito|barra|bebida|leite|cookie|granola|mingau|farelo|farinha|bolacha"),
    ("acucar-refinado", "açúcar refinado", "acucar refinado", "sache|cubo"),
    ("acucar-mascavo", "açúcar mascavo", "mascavo", "biscoito|bolo|rapadura"),
    ("cafe-em-po", "café torrado e moído", "cafe", "capsula|soluvel|instant|cappuccino|leite|grao|graos|bebida|pronto|biscoito|chocolate|descafeinado|dolce"),
    ("milho-em-conserva", "milho verde conserva", "milho", "pipoca|farinha|cuscuz|flocao|creme|pamonha|canjica|biscoito|salgadinho|seleta|ervilha|jardineira"),
    ("atum", "atum", "atum", "pate|salada|molho|sanduiche|ralado|com legumes"),
    ("pasta-de-amendoim", "pasta de amendoim", "pasta de amendoim|pasta amendoim|manteiga de amendoim", "chocolate|cookie|barra|bombom|pacoca|sache"),
    ("linhaca", "linhaça", "linhaca", "farinha|oleo|biscoito|pao|barra"),
    ("azeite", "azeite de oliva extra virgem", "azeite", "tempero|temperado|alho|ervas|spray|sache|dende"),
    ("oleo-de-soja", "óleo de soja", "oleo de soja|oleo soja", "composto"),
    ("mel", "mel", "^mel|mel de abelha|mel puro|mel silvestre|mel organico", "melancia|melao|caramel|melado|bala|biscoito|sache|propolis|cereal"),
    ("leite-integral", "leite integral", "leite.*integral|leite uht|leite longa vida", "po|em po|condensado|fermentado|achocolatado|chocolate|zero lactose|sem lactose|desnatado|semidesnatado|coco|amendoa|soja|aveia|biscoito|iogurte|bebida lactea|creme"),
    ("leite-desnatado", "leite desnatado", "leite.*desnatado", "po|em po|semidesnatado|condensado|fermentado|iogurte|bebida lactea"),
    ("iogurte-natural", "iogurte natural", "iogurte natural", "desnatado|grego|bebida|morango|frutas|mel|zero|light"),
    ("iogurte-natural-desnatado", "iogurte natural desnatado", "iogurte.*(desnatado|light|zero)", "grego|bebida|morango|frutas"),
    ("requeijao", "requeijão cremoso", "requeijao", "bisnaga|biscoito|pao|sache"),
    ("ricota", "ricota", "ricota", "creme|temperada|massa|recheio|lasanha|pao"),
    ("manteiga", "manteiga com sal", "manteiga", "amendoim|cacau|biscoito|bolacha|pao|ghee|garrafa|margarina|pipoca|cookie|sem sal"),
    ("queijo-parmesao", "queijo parmesão ralado", "parmesao", "biscoito|molho|pao|massa|salgadinho|torrada|batata"),
    ("pao-de-forma", "pão de forma integral", "pao de forma|pao integral|pao.*forma", "bisnag|hot dog|hamburguer|torrada|frances|queijo|bolo|doce"),
    ("agua-de-coco", "água de coco", "agua de coco", "po|sabor|com"),
    ("refrigerante-de-cola", "refrigerante cola", "cola", "guarana|laranja|limao|uva|cola de|bala|chiclete|zero cafeina"),
    ("refrigerante-de-guarana", "refrigerante guaraná", "guarana", "po|xarope|energetico|natural|semente|capsula|bala|suco|cha"),
    ("cerveja-pilsen", "cerveja pilsen", "cerveja|pilsen|pilsner", "sem alcool|zero|0,0|0.0|ipa|weiss|stout|malzbier"),
    ("acai", "polpa de açaí", "acai", "com |guarana|banana|morango|xarope|sorvete|po|bebida|tigela|granola|mix"),
]

TAMANHO = re.compile(r"(\d+(?:[.,]\d+)?)\s*(kg|g|gr|gramas|ml|l|lt|litros?)\b")


def sem_acento(texto):
    return "".join(c for c in unicodedata.normalize("NFD", texto or "") if unicodedata.category(c) != "Mn").lower()


def buscar(consulta, paginas=5):
    produtos = []
    for pagina in range(1, paginas + 1):
        parametros = urllib.parse.urlencode({
            "q": consulta + ' countries_tags:"en:brazil"',
            "fields": "code,product_name,quantity",
            "page_size": "100", "page": str(pagina), "langs": "pt"})
        pedido = urllib.request.Request("https://search.openfoodfacts.org/search?" + parametros,
                                        headers={"User-Agent": USER_AGENT})
        with urllib.request.urlopen(pedido, timeout=60) as resposta:
            dados = json.loads(resposta.read().decode("utf-8"))
        produtos += dados.get("hits", [])
        if pagina >= dados.get("page_count", 0):
            break
        time.sleep(0.5)
    return produtos


def normalizar(quantidade):
    m = TAMANHO.search(sem_acento(quantidade))
    if not m:
        return None
    n, u = float(m.group(1).replace(",", ".")), m.group(2)
    if u == "kg":
        return round(n * 1000), "g"
    if u in ("g", "gr", "gramas"):
        return round(n), "g"
    if u == "ml":
        return round(n), "ml"
    return round(n * 1000), "ml"


for chave, consulta, inclui, exclui in ITENS:
    inclui, exclui = re.compile(inclui), re.compile(exclui)
    vistos, contagem = set(), Counter()
    for produto in buscar(consulta):
        codigo = produto.get("code", "")
        if codigo in vistos or not codigo.startswith(("789", "790")):
            continue
        vistos.add(codigo)
        nome = sem_acento(produto.get("product_name", ""))
        if not inclui.search(nome) or exclui.search(nome):
            continue
        tamanho = normalizar(produto.get("quantity", ""))
        if tamanho:
            contagem[tamanho] += 1
    print(chave, sum(contagem.values()), [(f"{v} {u}", c) for (v, u), c in contagem.most_common(6)], flush=True)
