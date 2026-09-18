# Lacunas conhecidas (V2)

Decisões conscientes de escopo do MVP, registradas aqui para não serem esquecidas nem confundidas com bugs.

## Backend / API

- **Sem edição de perfil, objetivo, modalidades ou preferências já salvos.** Não existem endpoints `PUT`/`DELETE` para essas entidades — só é possível defini-las uma vez, no onboarding (objetivo tem histórico via novos registros, mas isso não substitui edição). V2: criar endpoints `PUT`/`DELETE` no backend e uma tela/seção no frontend para editá-los.
- **Senha não tem tamanho mínimo validado no backend** — a validação de 8 caracteres existe só no frontend (cadastro.html), o que significa que a API aceitaria uma senha mais curta se chamada diretamente.

## Cálculo de dieta / IA

- **Duração de sessão de treino fixa em 1 hora** (`DuracaoSessaoHorasMvp`, em `CalculadoraGastoCalorico`), usada para todas as modalidades e usuários igualmente. V2: permitir duração configurável por modalidade/usuário, para um cálculo de gasto calórico mais preciso.
- **Meta calórica é semanal, não diária por treino específico.** A meta diária calculada é a mesma em todos os 7 dias, baseada na frequência semanal de treino — não considera se aquele dia específico tem treino ou não. V2: calcular uma meta diferente para dias de treino vs. dias de descanso.

## Frontend

- **Imagem real ainda não substitui o placeholder da landing hero** — hoje é uma animação de espiral (SVG/JS), não uma foto de atleta em treino.
- **Login social (Google) planejado, não implementado.**
