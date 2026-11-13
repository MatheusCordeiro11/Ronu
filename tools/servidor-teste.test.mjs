// Testes do servidor de teste local (tools/servidor-teste.mjs).
// Rodar na raiz do repositÃ³rio:  node --test tools/servidor-teste.test.mjs
//
// O ponto principal: o servidor sÃ³ pode entregar arquivos de dentro da raiz.
// Cada teste monta uma raiz temporÃ¡ria com um arquivo "segredo" ao lado, fora
// dela, e tenta alcanÃ§Ã¡-lo de vÃ¡rios jeitos.

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { criarServidor, caminhoSeguro, cspLocal, API_LOCAL, RAIZ_PADRAO, lerConfig } from './servidor-teste.mjs';

let pasta, raiz, servidor, porta;

before(async () => {
  pasta = fs.mkdtempSync(path.join(os.tmpdir(), 'ronu-servidor-teste-'));
  raiz = path.join(pasta, 'frontend');
  fs.mkdirSync(path.join(raiz, 'landing', 'v1'), { recursive: true });
  fs.writeFileSync(path.join(pasta, 'segredo.txt'), 'SEGREDO');
  fs.writeFileSync(path.join(raiz, 'login.html'), '<p>login</p>');
  fs.writeFileSync(path.join(raiz, 'landing', 'v1', 'index.html'), '<p>landing</p>');
  fs.writeFileSync(
    path.join(raiz, 'vercel.json'),
    JSON.stringify({
      redirects: [{ source: '/', destination: '/landing/v1/', permanent: false }],
      headers: [
        {
          source: '/(.*)',
          headers: [
            { key: 'Content-Security-Policy', value: "default-src 'none'; connect-src 'self' https://api.exemplo.test" },
            { key: 'X-Frame-Options', value: 'DENY' },
          ],
        },
      ],
    }),
  );
  servidor = criarServidor(raiz);
  await new Promise((r) => servidor.listen(0, '127.0.0.1', r));
  porta = servidor.address().port;
});

after(() => {
  servidor.close();
  fs.rmSync(pasta, { recursive: true, force: true });
});

// Manda o caminho exatamente como escrito: o http.request do Node nÃ£o normaliza "..".
function pedir(caminho) {
  return new Promise((resolve, reject) => {
    const req = http.request({ host: '127.0.0.1', port: porta, path: caminho, method: 'GET' }, (res) => {
      let corpo = '';
      res.on('data', (d) => (corpo += d));
      res.on('end', () => resolve({ status: res.statusCode, corpo, cabecalhos: res.headers }));
    });
    req.on('error', reject);
    req.end();
  });
}

const tentativasDeFuga = [
  '/../segredo.txt',
  '/landing/../../segredo.txt',
  '/%2e%2e/segredo.txt',
  '/%2E%2E/segredo.txt',
  '/.%2e/segredo.txt',
  '/%2e%2e%2fsegredo.txt',
  '/%2e%2e%5csegredo.txt',
  '/..%5csegredo.txt',
  '/..\\segredo.txt',
  '/landing/%2e%2e/%2e%2e/segredo.txt',
  '/%252e%252e/segredo.txt',
  '//127.0.0.1/c$/segredo.txt',
  '/C:/Windows/win.ini',
  '/C%3A%5CWindows%5Cwin.ini',
  '/' + encodeURIComponent(path.join(pasta || os.tmpdir(), 'segredo.txt')),
  '/segredo.txt%00.html',
  '/%E0%A4%A',
];

test('nenhuma tentativa de sair da raiz entrega o arquivo de fora', async () => {
  for (const caminho of tentativasDeFuga) {
    const r = await pedir(caminho);
    assert.notEqual(r.status, 200, `${caminho} devolveu 200`);
    assert.ok(!r.corpo.includes('SEGREDO'), `${caminho} vazou o arquivo de fora`);
  }
});

test('caminho absoluto com a pasta temporÃ¡ria tambÃ©m Ã© recusado', async () => {
  const r = await pedir('/' + encodeURIComponent(path.join(pasta, 'segredo.txt')));
  assert.notEqual(r.status, 200);
  assert.ok(!r.corpo.includes('SEGREDO'));
});

test('atalho (junction/symlink) dentro da raiz apontando para fora nÃ£o vaza', async () => {
  // "junction" funciona no Windows sem permissÃ£o de administrador; nos outros sistemas o tipo Ã© ignorado.
  fs.symlinkSync(pasta, path.join(raiz, 'atalho'), 'junction');
  const r = await pedir('/atalho/segredo.txt');
  assert.equal(r.status, 404);
  assert.ok(!r.corpo.includes('SEGREDO'));
});

test('caminhoSeguro recusa ponto-ponto, barra invertida, dois-pontos e nulo', () => {
  for (const c of ['/..', '/a/../b', '/%2e%2e', '/a%5cb', '/C:/x', '/a%00b', '/./a', '/a//b', 'sem-barra', '//rede/x']) {
    assert.equal(caminhoSeguro(c), null, c);
  }
  assert.equal(caminhoSeguro('/landing/v1/'), 'landing/v1/');
  assert.equal(caminhoSeguro('/login.html'), 'login.html');
});

test('serve arquivos de dentro da raiz, com e sem .html', async () => {
  assert.equal((await pedir('/login.html')).corpo, '<p>login</p>');
  assert.equal((await pedir('/login')).corpo, '<p>login</p>');
  assert.equal((await pedir('/landing/v1/')).corpo, '<p>landing</p>');
});

test('aplica os cabeÃ§alhos do vercel.json e acrescenta a API local sÃ³ no connect-src', async () => {
  const r = await pedir('/login.html');
  assert.equal(r.cabecalhos['x-frame-options'], 'DENY');
  assert.equal(
    r.cabecalhos['content-security-policy'],
    `default-src 'none'; connect-src 'self' https://api.exemplo.test ${API_LOCAL}`,
  );
  // TambÃ©m nas respostas de erro.
  assert.equal((await pedir('/nao-existe.html')).cabecalhos['x-frame-options'], 'DENY');
});

test('redireciona a raiz como o vercel.json manda', async () => {
  const r = await pedir('/');
  assert.equal(r.status, 307);
  assert.equal(r.cabecalhos.location, '/landing/v1/');
});

test('cspLocal nÃ£o duplica a API local e nÃ£o mexe nas outras diretivas', () => {
  const v = `default-src 'none'; connect-src 'self' ${API_LOCAL}`;
  assert.equal(cspLocal(v), v);
  assert.equal(cspLocal("script-src 'self'"), "script-src 'self'");
});

test('o vercel.json de verdade nÃ£o lista o localhost (sÃ³ o servidor local acrescenta)', () => {
  const bruto = fs.readFileSync(path.join(RAIZ_PADRAO, 'vercel.json'), 'utf8');
  assert.ok(!bruto.includes('localhost'), 'frontend/vercel.json nÃ£o deve citar localhost');
  const { cabecalhos } = lerConfig(RAIZ_PADRAO);
  const csp = cabecalhos.flatMap((c) => c.valores).find((v) => /^content-security-policy/i.test(v.key));
  assert.ok(csp && csp.value.includes(API_LOCAL));
});
