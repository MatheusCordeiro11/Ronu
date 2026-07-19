# User Stories — Ronu (MVP)

## 0. Autenticação (cadastro e login)

**Como** visitante do Ronu
**Quero** criar uma conta e fazer login
**Para** acessar meus dados e dietas de forma segura

**Critérios de aceite:**
- Sistema permite cadastro com nome, email e senha
- Senha é armazenada com hash, nunca em texto plano
- Sistema permite login com email e senha
- Usuário não autenticado não acessa dados de outros usuários

---

## 1. Cadastro de dados corporais e objetivo

**Como** usuário do Ronu
**Quero** informar meus dados corporais (peso, altura, idade, sexo) e meu objetivo (ganhar, perder ou manter peso)
**Para que** o sistema calcule minha necessidade calórica de forma personalizada

**Critérios de aceite:**
- Sistema aceita peso, altura, idade e sexo biológico
- Sistema aceita escolha de um objetivo: ganhar peso, perder peso ou manter peso
- Dados ficam salvos vinculados ao usuário

---

## 2. Cadastro de modalidades e frequência de treino

**Como** usuário do Ronu
**Quero** informar quais modalidades pratico (jiu-jitsu, muay thai, academia, etc.) e a frequência semanal de cada uma
**Para que** o sistema calcule meu gasto calórico real de treino

**Critérios de aceite:**
- Sistema permite selecionar uma ou mais modalidades
- Sistema aceita frequência semanal por modalidade
- Sistema calcula gasto calórico total de treino baseado nessas informações
- Esse valor fica salvo vinculado ao usuário, disponível para ser usado na geração da dieta (story 4)

---

## 3. Cadastro de preferências alimentares

**Como** usuário do Ronu
**Quero** informar alimentos que gosto e que não gosto (ou não posso comer)
**Para que** a dieta gerada pela IA respeite minhas preferências e restrições

**Critérios de aceite:**
- Sistema permite cadastrar lista de alimentos preferidos
- Sistema permite cadastrar lista de alimentos a evitar
- Essas preferências ficam vinculadas ao usuário

---

## 4. Geração da dieta pela IA

**Como** usuário do Ronu
**Quero** que a IA gere uma dieta semanal personalizada com base nos meus dados, treinos e preferências
**Para** seguir uma alimentação alinhada ao meu objetivo sem precisar montar a dieta sozinho

**Critérios de aceite:**
- Sistema usa dados corporais + objetivo + gasto calórico de treino + preferências para montar o prompt
- IA retorna uma dieta estruturada para os 7 dias da semana
- Dieta gerada é salva vinculada ao usuário

---

## 5. Histórico de dietas

**Como** usuário do Ronu
**Quero** acessar as dietas geradas anteriormente
**Para** acompanhar minha evolução ao longo do tempo

**Critérios de aceite:**
- Sistema lista as dietas anteriores por data
- Usuário consegue abrir e visualizar uma dieta específica do histórico

---

## Ordem de dependência

```
Story 1 (dados corporais) ─┐
Story 2 (modalidades)      ├──> Story 4 (geração via IA) ──> Story 5 (histórico)
Story 3 (preferências)     ─┘
```