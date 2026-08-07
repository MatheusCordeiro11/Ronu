# Design da API — Ronu (MVP)

Lista de endpoints da API, definidos a partir das user stories (`docs/user-stories.md`) e do modelo de dados (`docs/der.png` / `docs/diagrama-classes.md`).

**Convenção geral:** todas as rotas exigem autenticação via token JWT (header `Authorization: Bearer <token>`), exceto `POST /api/auth/cadastro`, `POST /api/auth/login` e `GET /api/modalidades`. O `usuarioId` nunca é enviado pelo cliente — é sempre extraído do token, para evitar que um usuário manipule dados de outra conta.

---

## Autenticação

### `POST /api/auth/cadastro`
**Recebe:** `nome`, `email`, `senha`
**Devolve:** `id`, `nome`, `email` (a senha nunca é retornada, mesmo em hash)

### `POST /api/auth/login`
**Recebe:** `email`, `senha`
**Devolve:** `token` (JWT), `usuario { id, nome }`

---

## Objetivo / dados corporais (histórico)

### `POST /api/objetivos`
**Recebe:** `peso`, `objetivo`
**Devolve:** `id`, `peso`, `objetivo`, `dataRegistro`
*(`dataRegistro` é preenchida automaticamente pela API, não vem do cliente)*

### `GET /api/objetivos/atual`
**Devolve:** o registro mais recente de `ObjetivoUsuario` do usuário logado

---

## Modalidades

### `GET /api/modalidades`
**Devolve:** lista de todas as modalidades cadastradas no sistema (`id`, `nome`, `metReferencia`)
*(rota pública — é um catálogo do sistema, não dado de usuário)*

### `POST /api/usuarios/modalidades`
**Recebe:** `modalidadeId`, `frequenciaSemanal`
**Devolve:** `id`, `modalidade { id, nome }`, `frequenciaSemanal`

### `GET /api/usuarios/modalidades`
**Devolve:** lista das modalidades que o usuário logado pratica

---

## Preferências alimentares

### `POST /api/preferencias-alimentares`
**Recebe:** `alimento`, `tipo` (`preferido` ou `evitar`)
**Devolve:** `id`, `alimento`, `tipo`

### `GET /api/preferencias-alimentares`
**Devolve:** lista de preferências alimentares do usuário logado

---

## Dietas (geração via IA)

### `POST /api/dietas/gerar`
**Recebe:** nada — a API já busca internamente o objetivo/peso mais recente, as modalidades e as preferências do usuário logado
**Devolve:** `id`, `dataGeracao`, `conteudoJson` (a dieta estruturada em JSON)
*(regra de negócio: ao gerar uma nova dieta, se o usuário já tiver mais de 3 dietas salvas, a mais antiga é removida)*

### `GET /api/dietas/atual`
**Devolve:** a dieta mais recente do usuário logado

### `GET /api/dietas/historico`
**Devolve:** lista das últimas dietas geradas pelo usuário (até 3)

---

## Resumo de convenções aplicadas

- URLs representam **recursos** (substantivos, no plural), não ações
- Verbos HTTP seguem o padrão REST: `GET` para buscar, `POST` para criar
- Dados sensíveis (senha, token) nunca trafegam via query string / URL
- `usuarioId` é sempre extraído do token JWT, nunca enviado pelo cliente
- Catálogos do sistema (ex: modalidades) são públicos; dados pessoais exigem autenticação
