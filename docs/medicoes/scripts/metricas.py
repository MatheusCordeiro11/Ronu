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
        # Regra 15 (g10c): a última tentativa aceita a dieta abaixo do mínimo de refeições, com este aviso.
        m = re.search(r"Dieta aceita com menos refei..es que o m.nimo na .ltima tentativa: (.*)", linha)
        if m:
            eventos.append({"tipo": "aceita_abaixo", "dias": m.group(1).strip()})
            continue
        # Regra 16 (g10d): idem, sem fonte de gordura.
        m = re.search(r"Dieta aceita sem fonte de gordura suficiente na .ltima tentativa: (.*)", linha)
        if m:
            eventos.append({"tipo": "aceita_sem_gordura", "dias": m.group(1).strip()})
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


def medir_horarios(horarios, dias):
    """Horários das refeições contra a rotina estruturada do perfil (só A, C e F, desde o g10a).

    foraDaJanela: refeição antes de acordar ou depois de dormir. duranteTreino: refeição entre o início e o fim de
    um treino. Nos dias de treino, semRefeicaoAntes: nenhuma refeição nas 3 horas antes do início (a de 6h conta
    para um treino às 6h só se for antes dele); semRefeicaoDepois: nenhuma nas 2 horas depois do fim.
    """
    if not horarios:
        return {}
    acordar, dormir = hora(horarios["acordar"]), hora(horarios["dormir"])
    fora, durante, sem_antes, sem_depois, dias_treino = [], [], [], [], 0
    for dia in dias:
        nome = dia["diaSemana"]
        num = DIAS.index(nome) + 1 if nome in DIAS else None
        refs = [(hora(r.get("horario") or ""), r["nome"]) for r in dia["refeicoes"]]
        for h, n in refs:
            if h is not None and (h < acordar or h > dormir):
                fora.append(f"{nome} {n} {h // 60:02d}:{h % 60:02d}")
        for t in horarios["treinos"]:
            if num not in t["dias"]:
                continue
            dias_treino += 1
            ini = hora(t["inicio"])
            fim = ini + round(t["horas"] * 60)
            for h, n in refs:
                if h is not None and ini < h < fim:
                    durante.append(f"{nome} {n} {h // 60:02d}:{h % 60:02d} (treino {t['inicio']})")
            if not any(h is not None and ini - 180 <= h < ini for h, _ in refs) and ini - 180 >= acordar - 60:
                sem_antes.append(f"{nome} treino {t['inicio']}")
            if not any(h is not None and fim <= h <= fim + 120 for h, _ in refs):
                sem_depois.append(f"{nome} treino {t['inicio']}")
    return {"horarios": {"diasDeTreino": dias_treino, "foraDaJanela": fora, "duranteTreino": durante,
                         "semRefeicaoAntes": sem_antes, "semRefeicaoDepois": sem_depois}}


def medir(pid, dados, base, por_id, contaveis_ids):
    """As métricas de uma dieta (um arquivo P.json ou P-rN.json)."""
    perfil, status = dados["perfil"], dados["status"]
    r = {"status": status, "segundosCliente": dados["segundosCliente"], "uf": perfil["uf"]}
    if status != 200:
        return r
    dias = dados["resposta"]["dieta"]["dias"]
    itens = casados = refeicoes = refeicoes_base = com_id = 0
    contaveis = contaveis_fora = feijao_carioca_sul = macros_10_base = 0
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
        macros_10_base += all(abs(desvio_base[-1][k]) <= 10 for k in ("p", "c", "g"))
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

    # Regra 15 (g10c): mínimo de refeições pela meta do dia (5; 6 a partir de 3500 kcal). Medido em toda rodada,
    # também nas anteriores à regra, para comparar.
    abaixo_minimo = [f"{d['diaSemana']} ({len(d['refeicoes'])})" for d in dias
                     if len(d["refeicoes"]) < (6 if d["metaCalculada"]["calorias"] >= 3500 else 5)]
    # Regra 16 (g10d), a mesma checagem do C#: almoço e jantar sem fonte de gordura (óleos e gorduras, oleaginosas e
    # sementes, refogados) e o dia abaixo de 85% da gordura da meta, pela base.
    def gordo(al):
        i = por_id.get(al.get("alimentoId") or "") or base.get(norm(al["nome"])) or {}
        return i.get("categoria") in ("OleosEGorduras", "OleaginosasESementes") or i.get("id") in ("couve-refogada", "espinafre-refogado")
    sem_gordura = []
    for d in dias:
        aj = [x for x in d["refeicoes"] if re.search("almo|jant", norm(x["nome"]))]
        g = sum(((por_id.get(a.get("alimentoId") or "") or base.get(norm(a["nome"])) or {}).get("g", 0)) * float(a["quantidade"]) / 100
                for x in d["refeicoes"] for a in x["alimentos"])
        if not any(gordo(a) for x in aj for a in x["alimentos"]) and g < 0.85 * d["metaCalculada"]["gordurasG"]:
            sem_gordura.append(f"{d['diaSemana']} ({g:.0f} de {d['metaCalculada']['gordurasG']:.0f} g)")
    r.update({
        "diasSemFonteDeGordura": sem_gordura,
        "diasAbaixoDoMinimoDeRefeicoes": abaixo_minimo,
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
        "diasMacrosAte10PelaBase": macros_10_base,
        "refeicoesRepetidas": repetidas, "gostos": gostos, "violacoes": violacoes,
        "foraDaBase": dict(sorted(fora_da_base.items(), key=lambda x: -x[1])),
    })
    r.update(medir_horarios(perfil.get("horarios"), dias))
    return r


def main():
    pasta = sys.argv[1]
    base = carregar_base()
    por_id = {v["id"]: v for v in base.values()}
    contaveis_ids = carregar_contaveis()
    resultado = {"perfis": {}, "repeticoes": {}, "itensBase": len({v["id"] for v in base.values()})}
    for pid in PERFIS:
        medidas = []
        for s in sufixos(pasta):
            arquivo = os.path.join(pasta, f"{pid}-{s}.json" if s else f"{pid}.json")
            if not os.path.exists(arquivo):
                continue
            r = medir(pid, json.load(open(arquivo, encoding="utf-8")), base, por_id, contaveis_ids)
            resultado["repeticoes"][f"{pid}-{s or 'r1'}"] = r
            medidas.append(r)
        # Compatível com as rodadas de n=1: o perfil mostra a primeira repetição que gerou.
        if medidas:
            primeira = next((m for m in medidas if m["status"] == 200), medidas[0])
            resultado["perfis"][pid] = {**primeira, "pedidos": len(medidas)}

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
                if ev["tipo"] == "aceita_abaixo":
                    pedido["aceitaAbaixoDoMinimo"] = ev["dias"]
                elif ev["tipo"] == "aceita_sem_gordura":
                    pedido["aceitaSemFonteDeGordura"] = ev["dias"]
                elif ev["tipo"] == "tentativa_invalida":
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
    for pedido in pedidos:
        chave = f"{pedido['perfil']}-{pedido['repeticao']}"
        if chave in resultado["repeticoes"]:
            resultado["repeticoes"][chave].update({k: pedido[k] for k in ("tentativasGemini", "ms", "tokensEntrada", "tokensResposta", "invalidas") if k in pedido})

    # Taxa de álcool sem pedir: chamadas ao Gemini dos perfis sem bebida nas preferências (todos menos o G)
    # que vieram com bebida alcoólica, e pedidos que viraram 503 por isso.
    sem_alcool = [p for p in pedidos if p["perfil"] != "G"]
    chamadas = sum(p.get("tentativasGemini", 0) for p in sem_alcool)
    com_alcool = sum(sum("alco" in m for m in p["invalidas"]) for p in sem_alcool)
    resultado["alcoolSemPedir"] = {
        "chamadasGemini": chamadas, "chamadasComAlcool": com_alcool,
        "pedidos": len(sem_alcool), "pedidos503": sum(p.get("status") == 503 for p in sem_alcool),
    }

    # Resumo de todas as repetições que geraram (n=2 desde o g10a): médias e totais para comparar rodadas.
    geradas = [r for r in resultado["repeticoes"].values() if r["status"] == 200]
    if geradas:
        def media(f):
            return round(sum(f(r) for r in geradas) / len(geradas), 2)
        resultado["resumo"] = {
            "geradas": len(geradas), "repeticoes": len(resultado["repeticoes"]),
            "desvioPelaBase": {k: media(lambda r, k=k: r["desvioPelaBase"][k]) for k in ("kcal", "p", "c", "g")},
            "diasForaDe5PelaBase": sum(r["diasForaDe5PelaBase"] for r in geradas),
            "diasMacrosAte10PelaBase": sum(r["diasMacrosAte10PelaBase"] for r in geradas),
            "violacoes": {k: sum(len(r["violacoes"][k]) for r in geradas) for k in geradas[0]["violacoes"]},
            "refeicoesRepetidas": sum(len(r["refeicoesRepetidas"]) for r in geradas),
            "diasAbaixoDoMinimoDeRefeicoes": sum(len(r["diasAbaixoDoMinimoDeRefeicoes"]) for r in geradas),
            "recusasPorPoucasRefeicoes": sum(sum("menos refei" in m for m in p["invalidas"]) for p in resultado["pedidos"]),
            "aceitasAbaixoDoMinimo": sum("aceitaAbaixoDoMinimo" in p for p in resultado["pedidos"]),
            "diasSemFonteDeGordura": sum(len(r["diasSemFonteDeGordura"]) for r in geradas),
            "recusasSemFonteDeGordura": sum(sum("fonte de gordura" in m for m in p["invalidas"]) for p in resultado["pedidos"]),
            "aceitasSemFonteDeGordura": sum("aceitaSemFonteDeGordura" in p for p in resultado["pedidos"]),
            "recusasOutrosMotivos": sum(sum("menos refei" not in m and "fonte de gordura" not in m for m in p["invalidas"]) for p in resultado["pedidos"]),
            "pedidos503": sum(p.get("status") == 503 for p in resultado["pedidos"]),
            "alimentosDiferentesPorId": media(lambda r: r["alimentosDiferentesPorId"]),
            "contaveisForaDoMultiplo": sum(r["contaveisForaDoMultiplo"] for r in geradas),
            "horarios": {k: sum(len(r["horarios"][k]) for r in geradas if "horarios" in r)
                         for k in ("foraDaJanela", "duranteTreino", "semRefeicaoAntes", "semRefeicaoDepois")},
            "diasDeTreinoComRotina": sum(r["horarios"]["diasDeTreino"] for r in geradas if "horarios" in r),
        }

    json.dump(resultado, open(os.path.join(pasta, "metricas.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"metricas.json gravado em {pasta}")


if __name__ == "__main__":
    main()
