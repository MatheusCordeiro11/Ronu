# Design da API — Ronu (MVP)

Lista de endpoints da API, definidos a partir das user stories (`docs/user-stories.md`) e do modelo de dados (`docs/der.png` / `docs/diagrama-classes.md`).

**Convenção geral:** todas as rotas exigem autenticação via token JWT (header `Authorization: Bearer <token>`), exceto `POST /api/auth/cadastro`, `POST /api/auth/login`, `POST /api/auth/google`, as três rotas de redefinição de senha (`POST /api/auth/esqueci-senha`, `POST /api/auth/redefinir-senha/verificar` e `POST /api/auth/redefinir-senha`) e `GET /api/modalidades`. O `usuarioId` nunca é enviado pelo cliente — é sempre extraído do token, para evitar que um usuário manipule dados de outra conta. Sem token (ou com token expirado), a resposta é **401** — também quando o token foi emitido antes da última troca de senha da conta (claim `iat` contra `Usuario.SenhaAlteradaEm`; tokens sem `iat`, anteriores a essa regra, valem até expirar) ou quando a conta não existe mais.

**Erros:** todos os erros vêm com corpo `{ "mensagem": "<texto para o usuário>" }`, em português. Isso vale também para os da validação automática do modelo (**400**): campo obrigatório vazio ou nulo, `[MinLength]`, `[Range]` devolvem a **primeira** mensagem de erro, escrita no atributo do DTO (ex.: `{ "mensagem": "A senha deve ter pelo menos 8 caracteres." }`). Erros que não vêm de um atributo — JSON malformado ou com tipo errado, campo obrigatório ausente do JSON, corpo vazio, parâmetro de rota inválido — devolvem a genérica `{ "mensagem": "Requisição inválida. Confira os dados enviados." }`. Nenhuma mensagem padrão do ASP.NET (em inglês) chega ao cliente (`Validacao/RespostaValidacao.cs`). Exceções não tratadas viram **500** com `{ "mensagem" }` genérica em produção.

---

## Autenticação

**Email:** sempre gravado e comparado normalizado — sem espaços nas pontas e em minúsculas (`Services/NormalizacaoEmail.cs`) —, no cadastro, no login, no login com Google e na redefinição de senha. Há um índice único em `Usuarios.Email`: uma conta por email.

**Senha:** as mesmas regras no cadastro e na redefinição (`Validacao/RegrasSenha.cs`): de 8 a 64 caracteres, e no máximo 72 bytes (o limite do BCrypt; acentos e símbolos ocupam mais de 1 byte).

### `POST /api/auth/cadastro`
**Recebe:** `nome`, `email`, `senha` (8 a 64 caracteres), `estado` (UF)
**Devolve:** `id`, `nome`, `email` (normalizado; a senha nunca é retornada, mesmo em hash)
**Erros:** 409 se o email já está cadastrado (sem diferenciar maiúsculas); 400 se a senha for curta ou longa demais ou se algum campo vier vazio.

### `POST /api/auth/login`
**Recebe:** `email` (sem diferenciar maiúsculas), `senha`
**Devolve:** `token` (JWT, válido por 2 horas, com a claim `iat`), `usuario { id, nome }`, `precisaInformarEstado` (true quando a conta ainda não tem UF — contas via Google ou anteriores ao campo)
**Erros:** 401 com "Email ou senha inválidos." ou, se a conta foi criada pelo Google e não tem senha, com orientação para entrar com o Google.

### `POST /api/auth/google`
**Recebe:** `idToken` (o credential devolvido pelo botão do Google)
**Devolve:** o mesmo formato do `POST /api/auth/login`
*(a API valida a assinatura do token junto ao Google antes de usar qualquer dado dele. Se não existe conta com o email, cria uma sem senha; se existe, vincula o login com Google a ela. Por isso exige o email verificado pelo Google — `EmailVerified`)*
**Erros:** 401 se o token do Google for inválido ou se o email da conta Google não estiver verificado.

### `POST /api/auth/esqueci-senha`
**Recebe:** `email`
**Devolve:** **202** `{ mensagem }`, sempre com o mesmo texto — exista ou não a conta, tenha ela senha ou só login com Google, e também acima do limite por conta. Quem decide o que acontece é o `ServicoRedefinicaoSenha`:
- conta com senha: email com o link `{Frontend:UrlBase}/redefinir-senha.html#token=…`, válido por **30 minutos** e de uso único. O token vai no **fragmento** (`#`), que não chega a servidor nenhum nem vai no cabeçalho Referer; o banco guarda só o SHA-256 dele (tabela `PedidosRedefinicaoSenha`). Um pedido novo invalida os links anteriores;
- conta só com Google: email avisando que a conta usa o login com Google, sem link;
- email sem conta: nada é enviado nem gravado.

O email sai em segundo plano (a resposta não espera o SMTP). Limite por conta: 3 pedidos por hora e 10 por dia (acima disso, nada é enviado, mas a resposta não muda).
**Erros:** 400 se o email vier vazio; **429** `{ mensagem }` acima de 10 pedidos por hora do mesmo IP.

### `POST /api/auth/redefinir-senha/verificar`
**Recebe:** `token` (o do fragmento do link; no corpo, nunca na URL da API)
**Devolve:** 200 `{ mensagem, email }` se o link ainda vale — para a tela mostrar "link expirado" antes de a pessoa digitar a senha nova. O `email` é o da conta: a tela o coloca num campo escondido (`autocomplete="username"`) para o gerenciador de senhas do navegador associar a senha nova à conta certa. Só quem tem o token, que foi enviado para esse mesmo email, chega a essa resposta.
**Erros:** 400 `{ mensagem, motivo }`, com `motivo` = `invalido` (não existe, ou foi substituído por um pedido mais novo), `expirado` ou `usado`; **429** acima de 20 tentativas por hora do mesmo IP (somadas com o `POST /api/auth/redefinir-senha`).

### `POST /api/auth/redefinir-senha`
**Recebe:** `token`, `novaSenha` (8 a 64 caracteres)
**Devolve:** 200 `{ mensagem }`. A senha é trocada, o link fica usado, os outros links da conta são invalidados e **todos os tokens JWT emitidos antes da troca deixam de valer** (401). Não faz login automático: a pessoa entra com a senha nova.
**Erros:** 400 `{ mensagem, motivo }` como no `verificar`; 400 `{ mensagem }` se a senha nova for curta ou longa demais; **429** como no `verificar`.

---

## Perfil (dados pessoais)

### `GET /api/perfil`
**Devolve:** `altura` (cm), `sexo`, `dataNascimento`, `rotinaDiaria`, `orcamentoSemanal` — todos podem vir nulos enquanto o onboarding não os preencheu

### `PUT /api/perfil`
**Recebe:** `altura` (cm), `sexo` (`Masculino` ou `Feminino`), `dataNascimento` (obrigatórios); `rotinaDiaria` e `orcamentoSemanal` (opcionais; `orcamentoSemanal` aceita `economico`, `moderado` ou `sem_restricao`)
**Devolve:** o mesmo formato do `GET /api/perfil`
*(sobrescreve os valores atuais; não é histórico)*
**Erros:** 400 se o `orcamentoSemanal` não for um dos valores aceitos.

### `PUT /api/perfil/estado`
**Recebe:** `estado` (UF; a API ignora espaços e maiúsculas/minúsculas)
**Devolve:** `estado` (a sigla normalizada)
**Erros:** 400 se não for uma UF válida.

---

## Objetivo / dados corporais (histórico)

### `POST /api/objetivos`
**Recebe:** `peso`, `objetivo`, `aderencia` (opcional: `seguiu`, `comeu_mais`, `comeu_menos` ou `nao_seguiu`; nulo ou vazio = sem resposta)
**Devolve:** `id`, `peso`, `objetivo`, `dataRegistro`, `aderencia`, `registradoHoje`, `temDieta` (mesmo formato do `GET /api/objetivos/atual`)
*(`dataRegistro` é preenchida automaticamente pela API, em UTC, não vem do cliente. Se já existe um registro do usuário no mesmo dia — o dia no fuso do estado (UF) do usuário; sem estado, horário de Brasília —, ele é atualizado em vez de criar outro, inclusive a `aderencia`, que volta a nulo se não for enviada)*
**Erros:** 400 se a `aderencia` não for um dos valores aceitos.

### `GET /api/objetivos/atual`
**Devolve:** o registro mais recente de `ObjetivoUsuario` do usuário logado (`id`, `peso`, `objetivo`, `dataRegistro`, `aderencia`), mais `registradoHoje` (se esse registro é o de hoje, no fuso do estado do usuário, que o próximo POST vai sobrescrever — a mesma regra de "hoje" do POST) e `temDieta` (se o usuário já tem alguma dieta gerada)
**Erros:** 404 se o usuário ainda não registrou nenhum objetivo.

### `GET /api/objetivos/tendencia`
**Devolve:** lista de pontos do gráfico de peso, cada um com `data`, `pesoBruto` e `pesoTendencia` (peso suavizado)

### `DELETE /api/objetivos/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se o registro não existe ou é de outro usuário; 400 se for o único registro restante (o usuário precisa manter pelo menos um objetivo).

---

## Modalidades

### `GET /api/modalidades`
**Devolve:** lista de todas as modalidades cadastradas no sistema (`id`, `nome`, `metReferencia`)
*(rota pública — é um catálogo do sistema, não dado de usuário)*

### `POST /api/usuarios/modalidades`
**Recebe:** `modalidadeId`, `diasSemana` (lista de dias, de 1 = segunda a 7 = domingo, sem repetição, pelo menos um), `duracaoMediaHoras` (de 0,25 a 5)
**Devolve:** `id`, `modalidadeId`, `diasSemana`, `duracaoMediaHoras`
*(se o usuário já pratica a modalidade, atualiza dias e duração em vez de duplicar o vínculo)*
**Erros:** 404 se a modalidade não existe; 400 se um dia estiver fora de 1–7, se houver dia repetido, se a lista vier vazia ou se a duração estiver fora da faixa.

### `GET /api/usuarios/modalidades`
**Devolve:** lista das modalidades que o usuário logado pratica, cada uma com `id`, `modalidade { id, nome, metReferencia }`, `diasSemana`, `duracaoMediaHoras` e `frequenciaSemanal` (calculada a partir da quantidade de `diasSemana`, nunca armazenada)

### `DELETE /api/usuarios/modalidades/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se o vínculo não existe ou é de outro usuário.

---

## Preferências alimentares

### `POST /api/preferencias-alimentares`
**Recebe:** `alimento`, `tipo` (`preferido` ou `evitar`)
**Devolve:** `id`, `alimento`, `tipo`
*(se o usuário já tem uma preferência para o mesmo alimento, sem diferenciar maiúsculas/minúsculas, atualiza o `tipo` em vez de duplicar)*
**Erros:** 400 com `{ "mensagem": "Tipo de preferência inválido." }` se o `tipo` não for exatamente `preferido` ou `evitar` (sem normalizar maiúsculas nem espaços: `Evitar` é recusado).

### `GET /api/preferencias-alimentares`
**Devolve:** lista de preferências alimentares do usuário logado

### `DELETE /api/preferencias-alimentares/{id}`
**Devolve:** 204 sem corpo
**Erros:** 404 se a preferência não existe ou é de outro usuário.

---

## Dietas (geração via IA)

As três rotas devolvem a dieta no mesmo formato, `DietaResponse`:

- `dataGeracao`
- `dieta`
  - `dias`: os 7 dias da semana, cada um com `diaSemana`, `refeicoes` (cada refeição com `nome`, `horario` opcional, `alimentos [{ nome, quantidade, unidade }]` e `macros`), `totalDoDia`, `metaCalculada` e `metaElevadaPeloPiso`
  - `ajusteAdaptativo` (pode ser nulo): `percentual`, `motivo`, `aplicado`, `ritmoRealKgSemana`, `ritmoEsperadoKgSemana`, `pontosUsados`
- `macros`, `totalDoDia` e `metaCalculada` têm `calorias`, `proteinasG`, `carboidratosG` e `gordurasG`

### `POST /api/dietas/gerar`
**Recebe:** nada — a API já busca internamente o perfil, o objetivo/peso mais recente, as modalidades e as preferências do usuário logado
**Devolve:** `dataGeracao`, `dieta` (formato acima)
*(regra de negócio: o usuário fica com no máximo 3 dietas salvas — ao salvar uma nova, as mais antigas que passarem desse limite são removidas)*

**Respostas de erro** (corpo sempre `{ "mensagem": "<texto para o usuário>" }`):

- **400 Bad Request** — cadastro incompleto para gerar a dieta (nenhuma chamada à IA é feita):
  - perfil sem altura, sexo ou data de nascimento:
    `{ "mensagem": "Complete seu perfil (altura, sexo e data de nascimento) antes de gerar uma dieta." }`
  - nenhum objetivo registrado:
    `{ "mensagem": "Registre um objetivo antes de gerar uma dieta." }`
- **429 Too Many Requests** — limite de 3 gerações por usuário numa janela móvel de 1 hora (contado nas dietas salvas; nenhuma chamada à IA é feita). É a primeira checagem, antes das de 400. A mensagem diz quando a próxima vaga libera:
  `{ "mensagem": "Você já gerou 3 dietas na última hora, o limite por hora. Tente novamente em 12 minutos." }`
- **503 Service Unavailable** — a IA não gerou uma dieta utilizável: falha de comunicação com o Gemini (rede, status de erro, timeout) ou resposta inválida mesmo depois de uma nova tentativa (bloqueada, cortada, JSON inválido, semana sem exatamente 7 dias únicos com os nomes esperados). Nada é salvo e o detalhe vai só para o log do servidor:
  `{ "mensagem": "Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes." }`

### `GET /api/dietas/atual`
**Devolve:** a dieta mais recente do usuário logado (`dataGeracao`, `dieta`)
**Erros:** 404 se o usuário ainda não gerou nenhuma dieta.

### `GET /api/dietas/historico`
**Devolve:** lista das dietas salvas do usuário (até 3), da mais recente para a mais antiga, cada uma com `dataGeracao` e `dieta`

---

## Resumo de convenções aplicadas

- URLs representam **recursos** (substantivos, no plural), não ações
- Verbos HTTP seguem o padrão REST: `GET` para buscar, `POST` para criar, `PUT` para sobrescrever, `DELETE` para remover. Alguns `POST` atualizam o registro existente em vez de duplicar (objetivo do mesmo dia, mesma modalidade, mesmo alimento)
- Dados sensíveis (senha, token) nunca trafegam via query string / URL da API. A única exceção é o link de redefinição de senha, que leva o token no **fragmento** da URL do frontend (`#token=…`): o fragmento não é enviado a servidor nenhum, e a página manda o token à API no corpo de um POST
- `usuarioId` é sempre extraído do token JWT, nunca enviado pelo cliente
- Catálogos do sistema (ex: modalidades) são públicos; dados pessoais exigem autenticação
