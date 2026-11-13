// Ronu â€” Servidor de teste local do frontend com os cabeÃ§alhos de produÃ§Ã£o
//
// O Live Server nÃ£o envia cabeÃ§alhos, entÃ£o nÃ£o dÃ¡ para testar a CSP com ele.
// Este servidor serve a pasta frontend/ com os mesmos "headers" e "redirects"
// do frontend/vercel.json, lidos do prÃ³prio arquivo, para o teste local ver a
// mesma polÃ­tica que a Vercel aplica em produÃ§Ã£o.
//
// Uso (na raiz do repositÃ³rio):  node tools/servidor-teste.mjs [porta]
// Depois abrir http://127.0.0.1:5500/ (porta padrÃ£o 5500, a que o CORS da API
// local libera). Testes do prÃ³prio servidor: node --test tools/servidor-teste.test.mjs
//
// DiferenÃ§as de propÃ³sito em relaÃ§Ã£o Ã  produÃ§Ã£o:
// - O connect-src ganha http://localhost:5011 (a API local). A polÃ­tica de
//   produÃ§Ã£o nÃ£o lista o localhost; sÃ³ este servidor o acrescenta.
// - SÃ³ escuta em 127.0.0.1: nÃ£o fica exposto na rede.
// - Imita o cleanUrls de forma simples: /login serve login.html. NÃ£o refaz o
//   redirecionamento de /login.html para /login que a Vercel faz.
// - Dos padrÃµes de "source", sÃ³ entende os que jÃ¡ sÃ£o expressÃµes regulares
//   vÃ¡lidas (como "/(.*)") e redirecionamentos de caminho exato.
//
// SeguranÃ§a: sÃ³ entrega arquivos de dentro de frontend/. Recusa "..", ".",
// barra invertida, ":" (letra de unidade do Windows), byte nulo e as mesmas
// coisas escritas com %, e confere o caminho final (inclusive atalhos) antes
// de abrir o arquivo. Sem dependÃªncias: sÃ³ mÃ³dulos do Node.

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const AQUI = path.dirname(fileURLToPath(import.meta.url));
export const RAIZ_PADRAO = path.resolve(AQUI, '..', 'frontend');
export const API_LOCAL = 'http://localhost:5011';

const TIPOS = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.txt': 'text/plain; charset=utf-8',
  '.woff2': 'font/woff2',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.webp': 'image/webp',
};

// Transforma o caminho pedido (cru, ainda com %) num caminho relativo seguro
// dentro da raiz, ou null se ele tentar sair dela ou for estranho.
export function caminhoSeguro(caminhoCru) {
  if (typeof caminhoCru !== 'string' || !caminhoCru.startsWith('/')) return null;
  // "//servidor/x" vira caminho de rede no Windows; nenhum link do site usa isso.
  if (caminhoCru.startsWith('//')) return null;
  let decodificado;
  try {
    decodificado = decodeURIComponent(caminhoCru);
  } catch {
    return null; // % malformado
  }
  if (/[\\\0:]/.test(decodificado)) return null;
  const partes = decodificado.split('/').slice(1);
  if (partes.some((p) => p === '..' || p === '.')) return null;
  // Partes vazias no meio ("/a//b") tambÃ©m sÃ£o recusadas; sÃ³ a Ãºltima pode ser vazia ("/pasta/").
  if (partes.slice(0, -1).some((p) => p === '')) return null;
  return partes.join('/');
}

// Resolve o arquivo a servir para um caminho relativo jÃ¡ validado, ou null.
export function resolverArquivo(raiz, relativo) {
  const raizReal = fs.realpathSync(raiz);
  const candidatos = [];
  const base = path.resolve(raizReal, relativo);
  if (relativo === '' || relativo.endsWith('/')) {
    candidatos.push(path.join(base, 'index.html'));
  } else {
    candidatos.push(base);
    if (!path.extname(base)) candidatos.push(base + '.html'); // cleanUrls
  }
  for (const c of candidatos) {
    let real;
    try {
      real = fs.realpathSync(c);
    } catch {
      continue;
    }
    // Confere de novo depois de seguir atalhos: tem que continuar dentro da raiz.
    if (!real.startsWith(raizReal + path.sep)) continue;
    if (fs.statSync(real).isFile()) return real;
  }
  return null;
}

// Acrescenta a API local ao connect-src de uma CSP (sÃ³ no teste local).
export function cspLocal(valor) {
  return valor
    .split(';')
    .map((d) => {
      const t = d.trim();
      return /^connect-src(\s|$)/.test(t) && !t.split(/\s+/).includes(API_LOCAL) ? `${t} ${API_LOCAL}` : t;
    })
    .filter(Boolean)
    .join('; ');
}

export function lerConfig(raiz) {
  const bruto = JSON.parse(fs.readFileSync(path.join(raiz, 'vercel.json'), 'utf8'));
  const cabecalhos = (bruto.headers || []).map((regra) => ({
    padrao: new RegExp(`^${regra.source}$`),
    valores: regra.headers.map(({ key, value }) => ({
      key,
      value: /^content-security-policy(-report-only)?$/i.test(key) ? cspLocal(value) : value,
    })),
  }));
  const redirecionamentos = (bruto.redirects || []).map((r) => ({
    origem: r.source,
    destino: r.destination,
    status: r.statusCode || (r.permanent === false ? 307 : 308),
  }));
  return { cabecalhos, redirecionamentos };
}

export function criarServidor(raiz = RAIZ_PADRAO) {
  const config = lerConfig(raiz);

  return http.createServer((req, res) => {
    const caminhoCru = req.url.split('?')[0].split('#')[0];

    // Os cabeÃ§alhos vÃ£o em toda resposta, como na Vercel (inclusive 404).
    for (const regra of config.cabecalhos) {
      if (regra.padrao.test(caminhoCru)) for (const { key, value } of regra.valores) res.setHeader(key, value);
    }

    if (req.method !== 'GET' && req.method !== 'HEAD') {
      res.writeHead(405, { Allow: 'GET, HEAD' });
      return res.end();
    }

    const redir = config.redirecionamentos.find((r) => r.origem === caminhoCru);
    if (redir) {
      res.writeHead(redir.status, { Location: redir.destino });
      return res.end();
    }

    const relativo = caminhoSeguro(caminhoCru);
    if (relativo === null) {
      res.writeHead(400, { 'Content-Type': 'text/plain; charset=utf-8' });
      return res.end('Caminho recusado.');
    }

    const arquivo = resolverArquivo(raiz, relativo);
    if (!arquivo) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      return res.end('NÃ£o encontrado.');
    }

    res.writeHead(200, {
      'Content-Type': TIPOS[path.extname(arquivo).toLowerCase()] || 'application/octet-stream',
      'Cache-Control': 'no-store',
    });
    if (req.method === 'HEAD') return res.end();
    fs.createReadStream(arquivo).pipe(res);
  });
}

// SÃ³ sobe o servidor quando rodado direto (nÃ£o quando o teste importa o mÃ³dulo).
if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const porta = Number(process.argv[2] || 5500);
  criarServidor()
    .on('error', (e) => {
      console.error(e.code === 'EADDRINUSE' ? `A porta ${porta} jÃ¡ estÃ¡ em uso.` : e.message);
      process.exit(1);
    })
    .listen(porta, '127.0.0.1', () => {
      console.log(`Frontend em http://127.0.0.1:${porta}/ com os cabeÃ§alhos do frontend/vercel.json`);
      console.log(`(connect-src local inclui ${API_LOCAL}). Ctrl+C para parar.`);
    });
}
