/**
 * Spiral Animation — espiral de pontos em phyllotaxis (ângulo áureo),
 * usada como elemento visual no hero da landing page.
 * Baseada em https://21st.dev/@jahed/components/spiral-animation, adaptada
 * de React para JS puro (o projeto não usa React) e com prefers-reduced-motion.
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
      circle.setAttribute("opacity", reduceMotion ? "0.6" : "0.3");
      svg.appendChild(circle);

      if (!reduceMotion) {
        const begin = `${frac * duration}s`;

        const animR = document.createElementNS(SVG_NS, "animate");
        animR.setAttribute("attributeName", "r");
        animR.setAttribute(
          "values",
          `${dotRadius * 0.5};${dotRadius * 1.5};${dotRadius * 0.5}`
        );
        animR.setAttribute("dur", `${duration}s`);
        animR.setAttribute("begin", begin);
        animR.setAttribute("repeatCount", "indefinite");
        animR.setAttribute("calcMode", "spline");
        animR.setAttribute("keySplines", "0.4 0 0.6 1;0.4 0 0.6 1");
        circle.appendChild(animR);

        const animO = document.createElementNS(SVG_NS, "animate");
        animO.setAttribute("attributeName", "opacity");
        animO.setAttribute("values", "0.15;1;0.15");
        animO.setAttribute("dur", `${duration}s`);
        animO.setAttribute("begin", begin);
        animO.setAttribute("repeatCount", "indefinite");
        animO.setAttribute("calcMode", "spline");
        animO.setAttribute("keySplines", "0.4 0 0.6 1;0.4 0 0.6 1");
        circle.appendChild(animO);
      }
    }

    container.innerHTML = "";
    container.appendChild(svg);
  }

  window.initSpiralAnimation = initSpiralAnimation;
})();
