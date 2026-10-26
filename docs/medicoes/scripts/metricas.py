"""Métricas de uma rodada de medição (g8b, g9a...), sem nenhuma chamada ao Gemini.

Uso: python -I metricas.py <pasta da rodada>
Lê <pasta>/<perfil>.json (gravados por rodar_rodada.py) e <pasta>/log-api.txt (linhas do log da API),
e grava <pasta>/metricas.json.

"Pela base" = cada alimento casado pelo nome exato (sem acento, sem maiúsculas e sem a contagem entre
parênteses) com um item ou apelido de src/Ronu.Api/Data/BaseNutricional.cs, quantidade × valor por 100 g.
Uma refeição com algum item fora da base entra com os números que o Gemini declarou para ela; por isso a
cobertura (refeições inteiras pela base) vai junto, e o desvio pela base só é comparável entre rodadas com
cobertura parecida.
"""
import json
import os
import re
import sys
import unicodedata

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
BASE_CS = os.path.join(RAIZ, "src", "Ronu.Api", "Data", "BaseNutricional.cs")
MEDIDAS_CS = os.path.join(RAIZ, "src", "Ronu.Api", "Data", "BaseNutricionalMedidas.cs")
PERFIS = ["A", "B", "C", "D", "E", "F", "G"]
DIAS = ["Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado", "Domingo"]


def norm(texto):
    texto = unicodedata.normalize("NFD", texto.lower())
    texto = "".join(c for c in texto if unicodedata.category(c) != "Mn")
    texto = re.sub(r"\(.*?\)", "", texto)
    return re.sub(r"\s+", " ", texto).strip()


def carregar_base():
    fonte = open(BASE_CS, encoding="utf-8").read()
    padrao = re.compile(r'new\("([^"]+)", "([^"]+)", CategoriaAlimento\.\w+, "(g|ml)", FonteNutricional\.\w+, "[^"]*", "[^"]*", '
                        r'([\d.]+)m, ([\d.]+)m, ([\d.]+)m, ([\d.]+)m,')
    base = {}
    for m in padrao.finditer(fonte):
        item = {"id": m.group(1), "kcal": float(m.group(4)), "p": float(m.group(5)), "c": float(m.group(6)), "g": float(m.group(7))}
        base[norm(m.group(2))] = item
    # Categoria e alérgenos de cada item, para conferir o "evitar" pelo id (2b).
    por_id = {v["id"]: v for v in base.values()}
    for m in re.finditer(r'new\("([^"]+)", "[^"]+", CategoriaAlimento\.(\w+),.*?(?:true|false), [\d.]+m, (Alergenos\.\w+(?: \| Alergenos\.\w+)*)', fonte):
        if m.group(1) in por_id:
            por_id[m.group(1)]["categoria"] = m.group(2)
            por_id[m.group(1)]["alergenos"] = set(re.findall(r"Alergenos\.(\w+)", m.group(3)))
    # Apelidos regionais (ex.: Aipim cozido) apontam para o mesmo item.
    for bloco in re.finditer(r'new\("([^"]+)", "([^"]+)",[^\n]*\n(?:[^\n]*\n)?\s*Apelidos: \[(.*)$', fonte, re.M):
        alvo = base.get(norm(bloco.group(2)))
        for apelido in re.findall(r'new\("([^"]+)", \[', bloco.group(3)):
            if alvo:
                base[norm(apelido)] = alvo
    return base


def carregar_contaveis():
    """Ids dos itens com medida de unidade (EhUnidade) na POF: os contáveis."""
    fonte = open(MEDIDAS_CS, encoding="utf-8").read()
    return {bloco.split('"')[0] for bloco in re.split(r'\n\s*\["', fonte)[1:] if "EhUnidade: true" in bloco}


def palavras(regex, texto):
    return re.search(regex, norm(texto)) is not None


ALCOOL = r"\b(cerveja|vinho|vodca|vodka|cachaca|whisky|uisque|gin|rum|licor|chope|chopp|espumante)\b"
CAFEINA = r"\b(cafe|cha preto|cha mate|mate|cha verde|refrigerante de cola|cola|energetico)\b"
CARNE_VERMELHA = r"\b(bovin\w*|patinho|alcatra|acem|contrafile|maminha|mignon|coxao|lagarto|musculo|picanha|fraldinha|carne moida|carne vermelha|cupim|suin\w*|lombo|pernil|bisteca|porco)\b"
PEIXE = r"\b(peixe|tilapia|salmao|sardinha|atum|merluza|corvina|bacalhau|pescada|robalo|truta)\b"
LEITE = r"\bleite\b"
GOSTOS = {
    "A": {"Batata doce": r"\bbatata doce\b", "Tapioca": r"\btapioca\b"},
    "B": {"Banana": r"\bbanana\b"},
    "C": {"Whey protein": r"\bwhey\b", "Pasta de amendoim": r"\bpasta de amendoim\b"},
    "E": {"Ovo": r"\bovo\b"},
    "F": {"Iogurte": r"\biogurte\b", "Granola": r"\bgranola\b"},
    "G": {"Cerveja": r"\bcerveja\b", "Feijão": r"\bfeijao\b"},
}
EVITAR = {"B": ("Carne vermelha", CARNE_VERMELHA), "E": ("Leite", LEITE), "F": ("Peixe", PEIXE)}
# O mesmo "evitar" pela tabela do Ronu (CasamentoPreferencias), para itens da base: "leite" tira tudo o que
# tem lactose, não só o leite. Vale igual para o g8c e o g9a, para a comparação ser justa.
EVITAR_BASE = {
    "B": lambda i: i.get("categoria") in ("CarneBovina", "CarneSuina"),
    "E": lambda i: "Lactose" in i.get("alergenos", set()),
    "F": lambda i: i.get("categoria") == "Peixes",
}
MANDIOCA_ESPERADA = {"RJ": "aipim", "PE": "macaxeira", "BA": "macaxeira"}


def hora(h):
    try:
        hh, mm = h.split(":")
        return int(hh) * 60 + int(mm)
    except Exception:
        return None


def sufixos(pasta):
    """'' para a primeira passada (rodada.json) e 'r2', 'r3'... para as repetições, em ordem."""
    achados = [re.match(r"rodada(?:-(r\d+))?\.json$", n) for n in os.listdir(pasta)]
    return sorted((m.group(1) or "" for m in achados if m), key=lambda s: int(s[1:]) if s else 1)


def ler_log(caminho):
    if not os.path.exists(caminho):
        return []
    eventos = []
    for linha in open(caminho, encoding="utf-8"):
        m = re.search(r"Dieta gerada pelo Gemini \(([^)]*)\) em (\d+) ms \((\d+) tentativa\(s\)\); tokens: entrada (\d+), resposta (\d+)", linha)
        if m:
            eventos.append({"tipo": "ok", "ms": int(m.group(2)), "tentativas": int(m.group(3)), "entrada": int(m.group(4)), "resposta": int(m.group(5))})
            continue
        m = re.search(r"Resposta inv.lida do Gemini na tentativa (\d+)", linha)
        if m:
            eventos.append({"tipo": "tentativa_invalida"})
            continue
        m = re.search(r"\(ap.s nova tentativa\) ao gerar dieta", linha)
        if m:
            eventos.append({"tipo": "falhou"})
            continue
        m = re.search(r"RespostaIaInvalidaException: (.*)", linha)
        if m and eventos and eventos[-1]["tipo"] in ("tentativa_invalida", "falhou") and "motivo" not in eventos[-1]:
            eventos[-1]["motivo"] = m.group(1).strip()
    return eventos


def main():
    pasta = sys.argv[1]
    base = carregar_base()
    por_id = {v["id"]: v for v in base.values()}
    contaveis_ids = carregar_contaveis()
    resultado = {"perfis": {}, "itensBase": len({v["id"] for v in base.values()})}
    for pid in PERFIS:
        # A dieta medida é a da primeira repetição que gerou (P.json, depois P-r2.json...).
        arquivos = [f"{pid}-{s}.json" if s else f"{pid}.json" for s in sufixos(pasta)]
        tentativas = [json.load(open(os.path.join(pasta, a), encoding="utf-8")) for a in arquivos if os.path.exists(os.path.join(pasta, a))]
        dados = next((t for t in tentativas if t["status"] == 200), tentativas[0])
        perfil, status = dados["perfil"], dados["status"]
        r = {"status": status, "segundosCliente": dados["segundosCliente"], "uf": perfil["uf"], "pedidos": len(tentativas)}
        resultado["perfis"][pid] = r
        if status != 200:
            continue
        dias = dados["resposta"]["dieta"]["dias"]
        itens = casados = refeicoes = refeicoes_base = com_id = 0
        contaveis = contaveis_fora = feijao_carioca_sul = 0
        nomes = set()
        ids_diferentes = set()
        desvio_base_dias_inteiros = []
        desvio_decl, desvio_base = [], []
        fora_5_decl = fora_5_base = 0
        violacoes = {"alcool": [], "cafeinaNoite": [], "cafeinaFimTarde": [], "evitar": [], "refogadoComOleo": [], "regional": []}
        gostos = {g: 0 for g in GOSTOS.get(pid, {})}
        fora_da_base = {}
        for dia in dias:
            meta = dia["metaCalculada"]
            tot = {"kcal": 0.0, "p": 0.0, "c": 0.0, "g": 0.0}
            dia_inteiro = True
            for ref in dia["refeicoes"]:
                refeicoes += 1
                soma = {"kcal": 0.0, "p": 0.0, "c": 0.0, "g": 0.0}
                tudo_na_base = True
                tem_refogado = tem_oleo = False
                for al in ref["alimentos"]:
                    itens += 1
                    n = norm(al["nome"])
                    nomes.add(n)
                    # Desde a 2b-2 o item vem com o id da base; antes, casa pelo nome.
                    item = por_id.get(al.get("alimentoId") or "") or base.get(n)
                    com_id += bool(al.get("alimentoId"))
                    ids_diferentes.add(item["id"] if item else n)
                    if item and item["id"] in contaveis_ids:
                        contaveis += 1
                        contaveis_fora += "(" not in al["nome"]
                    if item:
                        casados += 1
                        fator = float(al["quantidade"]) / 100
                        for k in soma:
                            soma[k] += item[k] * fator
                    else:
                        tudo_na_base = False
                        fora_da_base[n] = fora_da_base.get(n, 0) + 1
                    rotulo = f"{dia['diaSemana']} {ref['nome']} {ref.get('horario')}: {al['nome']}"
                    if palavras(ALCOOL, al["nome"]) and pid != "G":
                        violacoes["alcool"].append(rotulo)
                    if palavras(ALCOOL, al["nome"]) and pid == "G" and dia["diaSemana"] not in ("Sábado", "Domingo"):
                        violacoes["alcool"].append(rotulo + " (fora do fim de semana)")
                    h = hora(ref.get("horario") or "")
                    nome_ref = norm(ref["nome"])
                    if palavras(CAFEINA, al["nome"]) and not palavras(r"\bdescafeinado\b", al["nome"]):
                        if "jantar" in nome_ref or "ceia" in nome_ref or (h is not None and h >= 18 * 60):
                            violacoes["cafeinaNoite"].append(rotulo)
                        elif h is not None and h >= 15 * 60:
                            violacoes["cafeinaFimTarde"].append(rotulo)
                    if pid in EVITAR and ((item and EVITAR_BASE[pid](item)) or (not item and palavras(EVITAR[pid][1], al["nome"]))):
                        violacoes["evitar"].append(rotulo)
                    if palavras(r"\b(refogad\w*|frit\w*)\b", al["nome"]):
                        tem_refogado = True
                    if palavras(r"\b(azeite|oleo)\b", al["nome"]):
                        tem_oleo = True
                    for g, rx in GOSTOS.get(pid, {}).items():
                        if palavras(rx, al["nome"]):
                            gostos[g] += 1
                    # Nomes regionais (regra 9).
                    # Desde a 2b os dois feijões entram em toda UF (valores diferentes): carioca no Sul é só registro.
                    if palavras(r"\bfeijao\b", al["nome"]) and perfil["uf"] in ("RS", "SC", "PR") and not palavras(r"\bfeijao preto\b", al["nome"]):
                        feijao_carioca_sul += 1
                    if palavras(r"\b(mandioca|aipim|macaxeira)\b", al["nome"]):
                        esperado = MANDIOCA_ESPERADA.get(perfil["uf"], "mandioca")
                        if not palavras(rf"\b{esperado}\b", al["nome"]):
                            violacoes["regional"].append(rotulo + f" (esperado {esperado})")
                if tem_refogado and tem_oleo:
                    violacoes["refogadoComOleo"].append(f"{dia['diaSemana']} {ref['nome']}")
                if tudo_na_base:
                    refeicoes_base += 1
                    usar = soma
                else:
                    dia_inteiro = False
                    m = ref["macros"]
                    usar = {"kcal": m["calorias"], "p": m["proteinasG"], "c": m["carboidratosG"], "g": m["gordurasG"]}
                for k in tot:
                    tot[k] += usar[k]
            dec = dia["totalDoDia"]
            pares_meta = {"kcal": meta["calorias"], "p": meta["proteinasG"], "c": meta["carboidratosG"], "g": meta["gordurasG"]}
            pares_dec = {"kcal": dec["calorias"], "p": dec["proteinasG"], "c": dec["carboidratosG"], "g": dec["gordurasG"]}
            desvio_decl.append({k: (pares_dec[k] - pares_meta[k]) / pares_meta[k] * 100 for k in pares_meta})
            desvio_base.append({k: (tot[k] - pares_meta[k]) / pares_meta[k] * 100 for k in pares_meta})
            fora_5_decl += abs(desvio_decl[-1]["kcal"]) > 5
            fora_5_base += abs(desvio_base[-1]["kcal"]) > 5
            if dia_inteiro:
                desvio_base_dias_inteiros.append(desvio_base[-1])

        # Refeição repetida: mesmo nome de refeição e mesmos alimentos (nome exato) do dia anterior.
        repetidas = []
        por_dia = {d["diaSemana"]: d for d in dias}
        for i in range(1, 7):
            ant, atual = por_dia.get(DIAS[i - 1]), por_dia.get(DIAS[i])
            if not ant or not atual:
                continue
            chaves_ant = {(norm(r["nome"]), tuple(sorted(a["nome"].strip().lower() for a in r["alimentos"]))) for r in ant["refeicoes"]}
            for r_ in atual["refeicoes"]:
                chave = (norm(r_["nome"]), tuple(sorted(a["nome"].strip().lower() for a in r_["alimentos"])))
                if chave in chaves_ant:
                    repetidas.append(f"{DIAS[i - 1]} → {DIAS[i]}: {r_['nome']}")

        def media_abs(lista, k):
            return round(sum(abs(x[k]) for x in lista) / len(lista), 1)

        r.update({
            "itens": itens, "itensNaBase": casados, "pctItensNaBase": round(casados / itens * 100, 1),
            "refeicoes": refeicoes, "pctRefeicoesInteirasNaBase": round(refeicoes_base / refeicoes * 100, 1),
            "alimentosDiferentes": len(nomes),
            "alimentosDiferentesPorId": len(ids_diferentes),
            "pctItensComAlimentoId": round(com_id / itens * 100, 1),
            "contaveis": contaveis, "contaveisForaDoMultiplo": contaveis_fora,
            "diasInteirosPelaBase": len(desvio_base_dias_inteiros),
            "desvioPelaBaseDiasInteiros": {k: media_abs(desvio_base_dias_inteiros, k) for k in ("kcal", "p", "c", "g")} if desvio_base_dias_inteiros else None,
            "feijaoCariocaNoSul": feijao_carioca_sul,
            "desvioDeclarado": {k: media_abs(desvio_decl, k) for k in ("kcal", "p", "c", "g")},
            "desvioPelaBase": {k: media_abs(desvio_base, k) for k in ("kcal", "p", "c", "g")},
            "diasForaDe5Declarado": fora_5_decl, "diasForaDe5PelaBase": fora_5_base,
            "refeicoesRepetidas": repetidas, "gostos": gostos, "violacoes": violacoes,
            "foraDaBase": dict(sorted(fora_da_base.items(), key=lambda x: -x[1])),
        })

    # Log: cada repetição (rodada.json, rodada-r2.json...) tem seu log (log-api.txt, log-api-r2.txt...), e os
    # eventos de cada log são atribuídos aos perfis na ordem em que aquela repetição os rodou.
    pedidos = []
    for sufixo in sufixos(pasta):
        marca = f"-{sufixo}" if sufixo else ""
        ordem = [x["perfil"] for x in json.load(open(os.path.join(pasta, f"rodada{marca}.json"), encoding="utf-8"))["perfis"]]
        fila = ler_log(os.path.join(pasta, f"log-api{marca}.txt"))
        for pid in ordem:
            pedido = {"perfil": pid, "repeticao": sufixo or "r1", "invalidas": []}
            while fila:
                ev = fila.pop(0)
                if ev["tipo"] == "tentativa_invalida":
                    pedido["invalidas"].append(ev.get("motivo", "?"))
                elif ev["tipo"] == "ok":
                    pedido.update({"status": 200, "tentativasGemini": ev["tentativas"], "ms": ev["ms"], "tokensEntrada": ev["entrada"], "tokensResposta": ev["resposta"]})
                    break
                elif ev["tipo"] == "falhou":
                    pedido["invalidas"].append(ev.get("motivo", "?"))
                    pedido.update({"status": 503, "tentativasGemini": 2})
                    break
            pedidos.append(pedido)
    resultado["pedidos"] = pedidos
    for pid in PERFIS:
        ok = [p for p in pedidos if p["perfil"] == pid and p.get("status") == 200]
        if ok:
            resultado["perfis"][pid].update({k: ok[0][k] for k in ("tentativasGemini", "ms", "tokensEntrada", "tokensResposta")})

    # Taxa de álcool sem pedir: chamadas ao Gemini dos perfis sem bebida nas preferências (todos menos o G)
    # que vieram com bebida alcoólica, e pedidos que viraram 503 por isso.
    sem_alcool = [p for p in pedidos if p["perfil"] != "G"]
    chamadas = sum(p.get("tentativasGemini", 0) for p in sem_alcool)
    com_alcool = sum(sum("alco" in m for m in p["invalidas"]) for p in sem_alcool)
    resultado["alcoolSemPedir"] = {
        "chamadasGemini": chamadas, "chamadasComAlcool": com_alcool,
        "pedidos": len(sem_alcool), "pedidos503": sum(p.get("status") == 503 for p in sem_alcool),
    }

    json.dump(resultado, open(os.path.join(pasta, "metricas.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"metricas.json gravado em {pasta}")


if __name__ == "__main__":
    main()
