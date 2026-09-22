# Lacunas conhecidas (V2)

Decisões conscientes de escopo do MVP, registradas aqui para não serem esquecidas nem confundidas com bugs.

## Resolvidas

- ~~Sem edição de perfil, modalidades ou preferências já salvos~~ — **Resolvido.** Perfil tem `GET`/`PUT /api/perfil`, com tela em `configuracoes.html`. Preferências e Modalidades têm CRUD completo (`GET`/`POST` upsert/`DELETE`), com guarda-rail contra remover a última modalidade (evita loop de onboarding).
- ~~Imagem real ainda não substitui o placeholder da landing hero~~ — **Resolvido.** Carrossel de ilustrações blackwork (6 modalidades), replicado em landing, login, cadastro e onboarding, cada tela com sua própria ordem de exibição.
- ~~Senha não tem tamanho mínimo validado no backend~~ — **Resolvido.** `[MinLength(8)]` em `CadastroRequest`, validado automaticamente pelo ASP.NET Core.
- ~~Objetivo ainda não é editável de fato~~ — **Resolvido.** `DELETE /api/objetivos/{id}`, com guarda-rail contra remover o último objetivo restante do usuário.
- ~~Duração de sessão de treino fixa em 1 hora~~ — **Resolvido.** `DuracaoMediaHoras` configurável por modalidade/usuário (0,25h–5h), substituindo a constante fixa.
- ~~Meta calórica é semanal, não diária por treino específico~~ — **Resolvido.** `FrequenciaSemanal` foi substituída por `DiasSemana` (array de dias específicos); a meta calórica agora é calculada por dia (TMB + gasto do treino daquele dia + ajuste por objetivo), casada com a resposta da IA pelo nome do dia, não pela posição.
- ~~Login social (Google) planejado, não implementado~~ — **Resolvido.** Login/cadastro com Google via `POST /api/auth/google`, com validação criptográfica real do token (biblioteca oficial `Google.Apis.Auth`) e vínculo automático por email a contas já existentes (sem duplicar conta).

## Frontend

- **Foto de perfil real não implementada.** O avatar no header usa iniciais do nome (calculadas dinamicamente), sem upload de imagem real. Em andamento: decidido usar Cloudinary (armazenamento gratuito de imagens) — implementação pausada temporariamente por instabilidade do serviço no momento da tentativa de configuração.
- **Meta calórica não é adaptativa ao progresso real.** O peso de tendência (GET /api/objetivos/tendencia) já existe e é exibido em progresso.html, mas a geração de dieta (GeradorDietaGemini) continua calculando a meta só por fórmula (Mifflin-St Jeor + gasto de treino + ajuste fixo por objetivo), sem considerar se o peso real do usuário está evoluindo como esperado. V2: implementar ajuste adaptativo (inspirado no MacroFactor — pesquisa registrada em conversa anterior), com guard-rails: só ajustar após ~14 dias de histórico real, regressão linear sobre o peso de tendência, passo de ajuste limitado (~5%/semana), e transparência ao usuário sobre por que a meta mudou.
