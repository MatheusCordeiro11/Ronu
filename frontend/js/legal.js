// Ronu — Páginas legais (privacidade.html e termos.html)
// Marca no sumário a seção que está sendo lida. Sem JavaScript, o sumário
// continua funcionando como lista de links.

document.addEventListener('DOMContentLoaded', () => {
  const links = Array.from(document.querySelectorAll('.lg-sumario a[href^="#"]'));
  const secoes = links.map((link) => document.getElementById(link.hash.slice(1))).filter(Boolean);
  if (!secoes.length || !('IntersectionObserver' in window)) return;

  const visiveis = new Set();
  function marcar() {
    // A primeira seção visível, na ordem da página.
    const atual = secoes.find((secao) => visiveis.has(secao)) || null;
    links.forEach((link) => {
      if (atual && link.hash === `#${atual.id}`) link.setAttribute('aria-current', 'true');
      else link.removeAttribute('aria-current');
    });
  }

  const observador = new IntersectionObserver((entradas) => {
    entradas.forEach((entrada) => {
      if (entrada.isIntersecting) visiveis.add(entrada.target);
      else visiveis.delete(entrada.target);
    });
    marcar();
  }, { rootMargin: '-10% 0px -55% 0px' });

  secoes.forEach((secao) => observador.observe(secao));
});
