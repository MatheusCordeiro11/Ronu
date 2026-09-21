# Lacunas conhecidas (V2)

Decisões conscientes de escopo do MVP, registradas aqui para não serem esquecidas nem confundidas com bugs.

## Resolvidas

- ~~Sem edição de perfil, modalidades ou preferências já salvos~~ — **Resolvido.** Perfil tem `GET`/`PUT /api/perfil`, com tela em `configuracoes.html`. Preferências e Modalidades têm CRUD completo (`GET`/`POST` upsert/`DELETE`), com telas próprias na mesma página. Modalidades tem guarda-rail contra remover a última (evita loop de onboarding).
- ~~Imagem real ainda não substitui o placeholder da landing hero~~ — **Resolvido.** Carrossel de ilustrações blackwork (6 modalidades: Jiu-jitsu, Boxe, Muay Thai, Musculação, Futebol, Basquete), também replicado em login.html e cadastro.html, cada tela com sua própria ordem de exibição.

## Backend / API

- **Objetivo ainda não é editável de fato.** `ObjetivoUsuario` continua histórico intencional (novo registro = novo objetivo atual), sem endpoint `PUT`/`DELETE` para corrigir ou apagar um registro específico do passado. Resolvido parcialmente na prática (registrar um novo objetivo já "substitui" o atual para fins de uso), mas não há como corrigir um erro de cadastro em um registro antigo.
- **Senha não tem tamanho mínimo validado no backend** — a validação de 8 caracteres existe só no frontend (cadastro.html), o que significa que a API aceitaria uma senha mais curta se chamada diretamente.

## Cálculo de dieta / IA

- **Duração de sessão de treino fixa em 1 hora** (`DuracaoSessaoHorasMvp`, em `CalculadoraGastoCalorico`), usada para todas as modalidades e usuários igualmente. V2: permitir duração configurável por modalidade/usuário, para um cálculo de gasto calórico mais preciso.
- **Meta calórica é semanal, não diária por treino específico.** A meta diária calculada é a mesma em todos os 7 dias, baseada na frequência semanal de treino — não considera se aquele dia específico tem treino ou não. V2: calcular uma meta diferente para dias de treino vs. dias de descanso.

## Frontend

- **Login social (Google) planejado, não implementado.**
- **Foto de perfil real não implementada.** O avatar no header usa iniciais do nome (calculadas dinamicamente), sem upload de imagem real — decisão consciente para não abrir uma frente de infraestrutura nova (upload de arquivo, armazenamento) fora do escopo da tela de Configurações.
