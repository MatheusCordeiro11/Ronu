/**
 * Spiral Animation — espiral de pontos em phyllotaxis (ângulo áureo),
 * usada como elemento visual no hero da landing page.
 * Baseada em https://21st.dev/@jahed/components/spiral-animation, adaptada
 * de React para JS puro (o projeto não usa React) e com prefers-reduced-motion.
 *
 * O pulso de cada ponto roda em CSS (@keyframes em landing.css via
 * transform/opacity), não em SMIL (<animate>): SMIL não entra no
 * compositor da GPU do jeito que transform/opacity entram, então com
 * centenas de pontos x 2 animações cada o navegador tinha que recalcular
 * estilo e repintar a cada frame. Aqui o JS só posiciona os pontos e grava
 * o atraso de cada um numa custom property; quem anima é o CSS.
 */
(function () {
  const SVG_NS = "http://www.w3.org/2000/svg";
  const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5)); // ~2.399963 rad (2π/φ²)

  function initSpiralAnimation(container, options = {}) {
    if (!container) return;

    const {
      totalDots = 500,
      size = 400,
      dotRadius = 2.5,
      margin = 4,
      duration = 4,
      dotColor = "currentColor",
    } = options;

    const reduceMotion = window.matchMedia(
      "(prefers-reduced-motion: reduce)"
    ).matches;

    const svg = document.createElementNS(SVG_NS, "svg");
    svg.setAttribute("viewBox", `0 0 ${size} ${size}`);
    svg.setAttribute("width", "100%");
    svg.setAttribute("height", "100%");
    svg.setAttribute("aria-hidden", "true");
    // Duração é a mesma pra todos os pontos — fica uma vez no <svg> e cada
    // círculo animado só herda; só o atraso (--ronu-spiral-atraso) varia
    // por ponto, pra manter o efeito de "acender" em espiral.
    svg.style.setProperty("--ronu-spiral-duracao", `${duration}s`);

    const center = size / 2;
    const maxRadius = center - margin - dotRadius;

    for (let i = 0; i < totalDots; i++) {
      const idx = i + 0.5;
      const frac = idx / totalDots;
      const r = Math.sqrt(frac) * maxRadius;
      const theta = idx * GOLDEN_ANGLE;
      const x = center + r * Math.cos(theta);
      const y = center + r * Math.sin(theta);

      const circle = document.createElementNS(SVG_NS, "circle");
      circle.setAttribute("cx", x.toFixed(2));
      circle.setAttribute("cy", y.toFixed(2));
      circle.setAttribute("r", dotRadius.toString());
      circle.setAttribute("fill", dotColor);

      if (reduceMotion) {
        circle.setAttribute("opacity", "0.6");
      } else {
        circle.classList.add("ronu-spiral-dot");
        circle.style.setProperty("--ronu-spiral-atraso", `${frac * duration}s`);
      }

      svg.appendChild(circle);
    }

    container.innerHTML = "";
    container.appendChild(svg);
  }

  window.initSpiralAnimation = initSpiralAnimation;
})();
