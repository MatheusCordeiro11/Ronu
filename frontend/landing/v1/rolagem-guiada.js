// Ronu — Liga a rolagem guiada da landing
//
// Carregado no <head>, sem defer, logo depois do tema.js: a classe precisa
// estar no <html> antes da primeira pintura, senão o layout pula. Com
// movimento reduzido a classe não entra e as seções ficam como blocos comuns.
// É um arquivo, e não um <script> inline, porque a CSP não libera script inline.
if (!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches)) {
  document.documentElement.classList.add('rolagem-guiada');
}
