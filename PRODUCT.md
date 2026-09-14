# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Praticantes de artes marciais e esportes de combate (jiu-jitsu, muay thai, e outras modalidades, incluindo treino de academia) que treinam com regularidade e querem alinhar a alimentação ao gasto real do treino, sem montar a dieta sozinhos. O produto trata todas as modalidades com o mesmo peso funcional; jiu-jitsu é usado como o exemplo/bandeira principal na comunicação e portfólio (copy, imagem hero, exemplos), sem tornar as demais modalidades cidadãs de segunda classe no produto.

## Product Purpose

Gerar dietas semanais personalizadas por IA para praticantes de artes marciais, a partir de dados corporais, objetivo (ganhar/perder/manter peso), gasto calórico real de treino (por modalidade e frequência semanal) e preferências alimentares. Sucesso = o usuário recebe uma dieta estruturada de 7 dias que reflete seu treino real e suas preferências, e consegue acompanhar o histórico das dietas geradas.

## Positioning

Apps de dieta genéricos calculam calorias a partir de níveis de atividade física vagos ("leve, moderado, intenso"). O Ronu parte do gasto calórico específico de cada modalidade de treino (MET de referência) combinado com a frequência semanal real do usuário — treino e nutrição calculados juntos, não em planilhas/tabelas separadas.

## Operating Context

- Fluxo: cadastro/login → dados corporais e objetivo → modalidades praticadas e frequência semanal → preferências alimentares (alimentos preferidos/a evitar) → geração de dieta via IA → histórico de dietas.
- API .NET (`src/Ronu.Api`) expõe endpoints REST autenticados via JWT; `usuarioId` nunca é enviado pelo cliente, sempre extraído do token.
- Catálogo de modalidades é público (`GET /api/modalidades`); todo o resto exige autenticação.
- Frontend estático (`frontend/`) já iniciado com uma landing page (`frontend/landing/v1`).

## Capabilities and Constraints

- Cadastro/login com hash de senha (nunca texto plano, nunca retornada pela API).
- Registro de dados corporais + objetivo (peso, altura, idade, sexo, objetivo), com histórico por data de registro.
- Registro de modalidades praticadas com frequência semanal; cálculo de gasto calórico total de treino.
- Registro de preferências alimentares (preferido / evitar).
- Geração de dieta semanal via IA a partir dos dados acima; regra de negócio: histórico limitado às últimas 3 dietas (a mais antiga é removida ao gerar uma nova).
- Terminologia do domínio: "modalidade" (tipo de treino/arte marcial), "objetivo" (ganhar/perder/manter peso), "preferência alimentar" (preferido/evitar).
- Em aberto: fluxos de recuperação de senha, edição/exclusão de dados cadastrados, e front-end das telas internas (dashboard, formulários de onboarding) ainda não implementados além da landing page.

## Brand Commitments

- Nome do produto: **Ronu** (wordmark em minúsculas, "ronu.").
- Tom de voz: sério, disciplinado, "para quem treina de verdade" — direto, sem enrolação, embasado (não motivacional genérico).
- Identidade visual já estabelecida em `frontend/css/styles.css` (ver comentário no arquivo): referência ao tatame de competição. Restrições explícitas do próprio time: sem gradientes roxos, sem sombras suaves genéricas, sem cantos muito arredondados.
- Paleta: fundo escuro (`#121820`/`#1B232D`), texto claro quente (`#EDE7DA`), acento dourado (`#C9A227`).
- Tipografia: Space Grotesk (display) + Inter (corpo).
- Jiu-jitsu é o exemplo/bandeira preferido em imagens e copy de portfólio/marketing, mesmo que o produto sirva todas as modalidades igualmente.

## Evidence on Hand

Nenhum conteúdo real ainda (sem fotos de atletas, depoimentos, dados de teste ou provas sociais). Não inventar testemunhos, benchmarks, números de usuários ou estudos de caso — usar placeholders explícitos onde uma imagem/prova social for necessária.

## Product Principles

1. O gasto calórico do treino real (modalidade + frequência) é o insumo central — nunca substituir por níveis de atividade genéricos.
2. Segurança de dados por padrão: nenhuma rota expõe ou aceita `usuarioId` do cliente; senha nunca trafega nem é retornada em texto plano.
3. MVP enxuto: histórico limitado (3 dietas), sem funcionalidades especulativas além das user stories confirmadas.
4. Tom sério e disciplinado acima de motivacional/genérico — o produto fala a quem já treina, não a quem está começando a se interessar por fitness.
5. Jiu-jitsu como vitrine, não como exclusividade — todas as modalidades recebem o mesmo tratamento funcional.

## Accessibility & Inclusion

Nenhum requisito específico de acessibilidade foi confirmado até o momento.
