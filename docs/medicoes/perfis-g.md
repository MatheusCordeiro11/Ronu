# Perfis das medições de geração de dieta (g8b em diante)

Perfis **fictícios** usados para medir a geração de dieta na API local, sempre na mesma ordem (A a G),
com `gemini-3.5-flash-lite` fixo e n=1 por perfil. O repositório é público: nada aqui é dado de pessoa real.

As respostas e os logs do g3 ao g8 se perderam (ficavam numa pasta temporária). A–E vêm dos testes
(`CalculadoraManutencaoTests.cs` e `PisoCarboidratoTests.cs`, perfis do g3); a modalidade de B não estava
registrada e foi escolhida agora. F e G são novos. As UFs e as preferências também são novas, então o g8b
**não** é comparável número a número com o g8: ele é a linha de base da fase 2b, e o g9a repete tudo igual.

## Perfis

Idade = idade na data da rodada; a data de nascimento é escolhida para dar essa idade em 2026-10-25.

| Perfil | Sexo | Idade (nascimento) | Peso | Altura | Objetivo | Treino | UF | Nível de atividade |
|---|---|---|---|---|---|---|---|---|
| A | Masculino | 30 (1996-05-10) | 80 kg | 178 cm | manter peso | Jiu-jitsu 1,5 h seg/ter/qui/sex | SP | sem resposta (×1,4) |
| B | Feminino | 28 (1998-03-15) | 58 kg | 162 cm | perder peso | Muay Thai 1 h ter/qui/sáb | RJ | sem resposta (×1,4) |
| C | Masculino | 31 (1995-02-20) | 95 kg | 180 cm | ganhar peso | Jiu-jitsu 1,5 h seg a sex + Musculação 1 h seg/qua/sex | RS | sem resposta (×1,4) |
| D | Feminino | 36 (1990-06-01) | 90 kg | 160 cm | perder peso | não treina | MG | sem resposta (×1,4) |
| E | Masculino | 30 (1996-05-10) | 80 kg | 178 cm | perder peso | Jiu-jitsu 1,5 h seg/ter/qui/sex | PE | sem resposta (×1,4) |
| F | Feminino | 24 (2002-04-12) | 63 kg | 168 cm | ganhar peso | Judô 1,5 h seg/qua/sex + Corrida 45 min sáb | SC | sentado (×1,30) |
| G | Masculino | 47 (1979-08-30) | 74 kg | 172 cm | manter peso | Boxe 1 h ter/qui + Natação 1 h sáb | BA | braçal (×1,75) |

O que cada perfil cobre:

- **A / E:** o mesmo homem, mantendo e perdendo peso (efeito só do objetivo).
- **B:** mulher leve com déficit e dias de descanso (metas baixas); aipim (RJ).
- **C:** duas modalidades no mesmo dia, superávit, metas altas; feijão preto (RS). Foi o perfil que recebeu cerveja sem pedir no g7 e no g8.
- **D:** IMC alto, sem treino, déficit grande (o perfil do pior caso de gordura no g6).
- **E:** macaxeira (PE).
- **F:** mulher que treina e ganha peso, luta + corrida, nível de atividade baixo; feijão preto (SC).
- **G:** mais velho, boxe e natação (modalidade de luta diferente do jiu-jitsu e uma de condicionamento), nível braçal; macaxeira (BA).

Rotina diária e orçamento semanal ficam **vazios** em todos (sem variável a mais entre as rodadas).

## Preferências (idênticas no g8b e no g9a)

| Perfil | Gosto | Evitar | O que testa |
|---|---|---|---|
| A | Batata doce, Tapioca | — | "gosto" que está na base |
| B | Banana | Carne vermelha | "evitar" de categoria |
| C | Whey protein, Pasta de amendoim | — | "gosto" fora da base (whey) e na base (pasta de amendoim); álcool sem pedir |
| D | — | — | sem nenhuma preferência |
| E | Ovo | Leite | "evitar" de um item (casa com os dois leites da base) |
| F | Iogurte, Granola | Peixe | "gosto" fora da base (granola) e "evitar" de categoria |
| G | Cerveja, Feijão | — | álcool nas preferências (regra 8); a base tem "Cerveja pilsen" |

**Cerveja:** a base tem um item alcoólico, "Cerveja pilsen" (`cerveja-pilsen`, grupo `BebidasAlcoolicas`). Na
fase 2b ele só entra na lista (prompt e enum) de quem marcou cerveja como "gosto": no g9a, só o G pode
recebê-la, e qualquer cerveja nos outros perfis é falha. O G testa o álcool, não o "gosto fora da base".

## Contas

Uma conta descartável por perfil e por rodada: `g8b-a-20261025@exemplo.com`, `g9a-a-AAAAMMDD@exemplo.com` etc.
Senha gerada na hora e não guardada (as contas são apagadas depois, no banco local, numa transação
filtrando por id e email, com a lista mostrada antes). Tudo pela API HTTP local, na ordem do onboarding:
cadastro → estado → perfil → objetivo (peso) → modalidades → preferências → `POST /dietas/gerar`.
