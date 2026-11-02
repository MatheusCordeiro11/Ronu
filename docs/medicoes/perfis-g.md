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

Orçamento semanal fica **vazio** em todos (sem variável a mais entre as rodadas). A rotina diária ficou vazia até o
g9c; desde o g10a, A, C e F têm uma rotina fictícia, para medir os horários das refeições (o prompt reduzido tira a
rotina, e o g10a é a linha de base dele):

| Perfil | Rotina (texto enviado) | O que o `metricas.py` confere |
|---|---|---|
| A | "Acordo às 6h, trabalho das 8h às 17h e treino jiu-jitsu às 19h30 na segunda, terça, quinta e sexta. Durmo às 23h." | acordar 6h, dormir 23h, treino 19h30–21h |
| C | "Acordo às 5h30. Segunda, quarta e sexta faço musculação às 6h. Trabalho das 8h às 17h e treino jiu-jitsu às 19h de segunda a sexta. Durmo às 22h30." | acordar 5h30, dormir 22h30, musculação 6h–7h, jiu-jitsu 19h–20h30 |
| F | "Acordo às 7h, estudo de manhã e trabalho das 13h às 18h. Judô às 20h na segunda, quarta e sexta, e corrida no sábado às 8h. Durmo à meia-noite." | acordar 7h, dormir 24h, judô 20h–21h30, corrida 8h–8h45 |

Métricas de horário (só A, C e F): refeição fora da janela acordado; refeição durante o treino; dia de treino sem
refeição nas 3 horas antes do início; dia de treino sem refeição nas 2 horas depois do fim.

Desde o g10b, os mesmos horários também vão para a API como dados estruturados (`horarioAcordar` e `horarioDormir` no
perfil, `horarioInicio` em cada modalidade; a meia-noite do F vai como "00:00"). O prompt reduzido os usa no lugar da
rotina em texto, que continua gravada mas não vai mais ao Gemini.

**g10a r3 e g10c (n=3).** A 3ª repetição do g10a rodou com o prompt atual numa cópia do commit `04bbd05`
(`git worktree`), para comparar com o g10c em n=3. No g10c, o C da repetição 1 recebeu um 503 do próprio Gemini
(serviço indisponível, sem nova tentativa por desenho): a conta foi apagada e o C rodou de novo no lote seguinte, e
o 503 não entra na comparação de qualidade. O `metricas.py` passou a contar os dias abaixo do mínimo de refeições
(regra 15 do g10c: 5 por dia, 6 a partir de 3500 kcal), as recusas por isso e as dietas aceitas abaixo do mínimo na
última tentativa.

**g10d (n=3).** Regra 16: fontes de gordura da lista para chegar à gordura da meta. Recusa na 1ª tentativa o dia com
almoço e jantar sem fonte de gordura (óleos e gorduras, oleaginosas e sementes, refogados) e abaixo de 85% da gordura
da meta; na última, aceita e registra. Calibrada para não recusar nenhum dia do g10a (0 de 147) e pegar os 9 do g10c.
O `metricas.py` conta esses dias, as recusas e as aceitas.

**n=2 desde o g10a.** Cada perfil roda duas vezes (`P.json` e `P-r2.json`), e o `metricas.py` mede as duas e tira o
`resumo` (médias e totais). O cadastro tem limite de 5 contas por hora por IP, em memória: cada repetição roda em
dois lotes (A a E, depois F e G), com a API local reiniciada antes de cada lote (configuração `api-local-medicao` do
`.claude/launch.json`, que grava o log). O log de cada lote é somado ao `log-api.txt` (ou `log-api-r2.txt`) na
ordem em que rodou.

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

Uma conta descartável por perfil e por rodada: `g10a-a-20261102@exemplo.test`, `g10b-a-r2-AAAAMMDD@exemplo.test` etc.
O domínio é `@exemplo.test` desde o g10 (o `.test` é reservado e não existe na internet). Até o g10a as contas
usavam `@exemplo.com`, que é um domínio de verdade; nos arquivos dessas rodadas só o domínio foi trocado depois,
num commit à parte. Desde a LGPD base, o cadastro manda o aceite do consentimento geral.
Senha gerada na hora e não guardada (as contas são apagadas depois, no banco local, numa transação
filtrando por id e email, com a lista mostrada antes). Tudo pela API HTTP local, na ordem do onboarding:
cadastro (com o aceite) → estado → perfil (com a rotina, se houver) → objetivo (peso) → modalidades → preferências → `POST /dietas/gerar`.
