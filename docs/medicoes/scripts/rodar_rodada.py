"""Roda uma rodada de medição (g8b, g9a...) contra a API LOCAL.

Uso: python -I rodar_rodada.py <rodada> <pasta de saída> [perfis] [sufixo]
Ex.: python -I docs/medicoes/scripts/rodar_rodada.py g8b docs/medicoes/g8b
     python -I docs/medicoes/scripts/rodar_rodada.py g8b docs/medicoes/g8b EF r2
O segundo exemplo repete só E e F, com contas novas (g8b-e-r2-...) e arquivos E-r2.json, F-r2.json e
rodada-r2.json, sem sobrescrever os da primeira tentativa.

Cria uma conta descartável por perfil (docs/medicoes/perfis-g.md), na ordem A a G, preenche o cadastro pela
API, na ordem do onboarding, e gera uma dieta. Grava a resposta bruta de cada perfil em <pasta>/<perfil>.json
e um resumo em <pasta>/rodada.json. A senha é gerada na hora e não é gravada em lugar nenhum.

Só fala com http://localhost:5011: nunca com a produção.
"""
import datetime
import json
import secrets
import sys
import time
import urllib.error
import urllib.request

API = "http://localhost:5011/api"

# Mesmos dados de docs/medicoes/perfis-g.md. Dias: 1 = segunda ... 7 = domingo.
PERFIS = [
    {"id": "A", "sexo": "Masculino", "nascimento": "1996-05-10", "peso": 80, "altura": 178, "objetivo": "manter peso",
     "uf": "SP", "nivel": None, "modalidades": [("Jiu-jitsu", [1, 2, 4, 5], 1.5)],
     "gosto": ["Batata doce", "Tapioca"], "evitar": []},
    {"id": "B", "sexo": "Feminino", "nascimento": "1998-03-15", "peso": 58, "altura": 162, "objetivo": "perder peso",
     "uf": "RJ", "nivel": None, "modalidades": [("Muay Thai", [2, 4, 6], 1.0)],
     "gosto": ["Banana"], "evitar": ["Carne vermelha"]},
    {"id": "C", "sexo": "Masculino", "nascimento": "1995-02-20", "peso": 95, "altura": 180, "objetivo": "ganhar peso",
     "uf": "RS", "nivel": None, "modalidades": [("Jiu-jitsu", [1, 2, 3, 4, 5], 1.5), ("Musculação", [1, 3, 5], 1.0)],
     "gosto": ["Whey protein", "Pasta de amendoim"], "evitar": []},
    {"id": "D", "sexo": "Feminino", "nascimento": "1990-06-01", "peso": 90, "altura": 160, "objetivo": "perder peso",
     "uf": "MG", "nivel": None, "modalidades": [],
     "gosto": [], "evitar": []},
    {"id": "E", "sexo": "Masculino", "nascimento": "1996-05-10", "peso": 80, "altura": 178, "objetivo": "perder peso",
     "uf": "PE", "nivel": None, "modalidades": [("Jiu-jitsu", [1, 2, 4, 5], 1.5)],
     "gosto": ["Ovo"], "evitar": ["Leite"]},
    {"id": "F", "sexo": "Feminino", "nascimento": "2002-04-12", "peso": 63, "altura": 168, "objetivo": "ganhar peso",
     "uf": "SC", "nivel": "sentado", "modalidades": [("Judô", [1, 3, 5], 1.5), ("Corrida", [6], 0.75)],
     "gosto": ["Iogurte", "Granola"], "evitar": ["Peixe"]},
    {"id": "G", "sexo": "Masculino", "nascimento": "1979-08-30", "peso": 74, "altura": 172, "objetivo": "manter peso",
     "uf": "BA", "nivel": "bracal", "modalidades": [("Boxe", [2, 4], 1.0), ("Natação", [6], 1.0)],
     "gosto": ["Cerveja", "Feijão"], "evitar": []},
]


def chamar(metodo, caminho, corpo=None, token=None, timeout=300):
    dados = None if corpo is None else json.dumps(corpo).encode("utf-8")
    req = urllib.request.Request(API + caminho, data=dados, method=metodo)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            texto = resp.read().decode("utf-8")
            return resp.status, (json.loads(texto) if texto else None)
    except urllib.error.HTTPError as e:
        texto = e.read().decode("utf-8")
        try:
            return e.code, json.loads(texto)
        except ValueError:
            return e.code, texto


def exigir(status, corpo, esperado, passo):
    if status not in esperado:
        raise RuntimeError(f"{passo}: status {status}: {corpo}")


def main():
    rodada, pasta = sys.argv[1], sys.argv[2]
    escolhidos = sys.argv[3] if len(sys.argv) > 3 else "ABCDEFG"
    sufixo = sys.argv[4] if len(sys.argv) > 4 else ""
    marca = f"-{sufixo}" if sufixo else ""
    data = datetime.date.today().strftime("%Y%m%d")
    status, modalidades = chamar("GET", "/modalidades")
    exigir(status, modalidades, (200,), "modalidades")
    id_modalidade = {m["nome"]: m["id"] for m in modalidades}

    resumo = []
    for p in (p for p in PERFIS if p["id"] in escolhidos):
        email = f"{rodada}-{p['id'].lower()}{marca}-{data}@exemplo.com"
        senha = secrets.token_urlsafe(18)  # não é gravada: a conta é descartável
        s, c = chamar("POST", "/auth/cadastro", {"nome": f"Perfil {p['id']} {rodada}", "email": email, "senha": senha, "estado": p["uf"]})
        exigir(s, c, (200, 201), f"{p['id']} cadastro")
        s, c = chamar("POST", "/auth/login", {"email": email, "senha": senha})
        exigir(s, c, (200,), f"{p['id']} login")
        token, usuario_id = c["token"], c["usuario"]["id"]

        perfil = {"altura": p["altura"], "sexo": p["sexo"], "dataNascimento": p["nascimento"]}
        if p["nivel"]:
            perfil["nivelAtividade"] = p["nivel"]
        s, c = chamar("PUT", "/perfil", perfil, token)
        exigir(s, c, (200,), f"{p['id']} perfil")
        s, c = chamar("POST", "/objetivos", {"peso": p["peso"], "objetivo": p["objetivo"]}, token)
        exigir(s, c, (200, 201), f"{p['id']} objetivo")
        for nome, dias, horas in p["modalidades"]:
            s, c = chamar("POST", "/usuarios/modalidades", {"modalidadeId": id_modalidade[nome], "diasSemana": dias, "duracaoMediaHoras": horas}, token)
            exigir(s, c, (200, 201), f"{p['id']} modalidade {nome}")
        for tipo, lista in (("preferido", p["gosto"]), ("evitar", p["evitar"])):
            for alimento in lista:
                s, c = chamar("POST", "/preferencias-alimentares", {"alimento": alimento, "tipo": tipo}, token)
                exigir(s, c, (200, 201), f"{p['id']} preferência {alimento}")

        inicio = time.monotonic()
        s, c = chamar("POST", "/dietas/gerar", token=token)
        segundos = round(time.monotonic() - inicio, 1)
        with open(f"{pasta}/{p['id']}{marca}.json", "w", encoding="utf-8") as f:
            json.dump({"perfil": p, "status": s, "segundosCliente": segundos, "resposta": c}, f, ensure_ascii=False, indent=1)
        resumo.append({"perfil": p["id"], "usuarioId": usuario_id, "email": email, "status": s, "segundosCliente": segundos})
        print(f"{p['id']}: status {s} em {segundos} s", flush=True)

    with open(f"{pasta}/rodada{marca}.json", "w", encoding="utf-8") as f:
        json.dump({"rodada": rodada, "data": data, "perfis": resumo}, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
