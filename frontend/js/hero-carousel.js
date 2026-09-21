/*
  Ronu — Carrossel de imagens do hero.
  HTML/CSS/JS puro, sem dependências externas.
*/
function initHeroCarousel(root, options) {
  if (!root) return;

  var settings = Object.assign({ autoDelay: 5000, resumeDelay: 8000 }, options || {});

  var slides = Array.prototype.slice.call(root.querySelectorAll('.hero-carousel-slide'));
  var dots = Array.prototype.slice.call(root.querySelectorAll('.hero-carousel-dot'));

  if (!slides.length) return;

  var current = slides.findIndex(function (slide) {
    return slide.classList.contains('is-active');
  });
  if (current < 0) current = 0;

  var autoTimer = null;
  var resumeTimer = null;
  var reduceMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');

  function goTo(index) {
    var next = (index + slides.length) % slides.length;
    if (next === current) return;

    slides[current].classList.remove('is-active');
    if (dots[current]) {
      dots[current].classList.remove('is-active');
      dots[current].setAttribute('aria-selected', 'false');
    }

    current = next;

    slides[current].classList.add('is-active');
    if (dots[current]) {
      dots[current].classList.add('is-active');
      dots[current].setAttribute('aria-selected', 'true');
    }
  }

  function stopAuto() {
    if (autoTimer) {
      window.clearInterval(autoTimer);
      autoTimer = null;
    }
  }

  function startAuto() {
    if (reduceMotionQuery.matches || slides.length < 2) return;
    stopAuto();
    autoTimer = window.setInterval(function () {
      goTo(current + 1);
    }, settings.autoDelay);
  }

  function pauseThenResume() {
    stopAuto();
    if (resumeTimer) window.clearTimeout(resumeTimer);
    if (reduceMotionQuery.matches) return;
    resumeTimer = window.setTimeout(startAuto, settings.resumeDelay);
  }

  dots.forEach(function (dot, index) {
    dot.addEventListener('click', function () {
      goTo(index);
      pauseThenResume();
    });
  });

  reduceMotionQuery.addEventListener('change', function () {
    if (reduceMotionQuery.matches) {
      stopAuto();
      if (resumeTimer) window.clearTimeout(resumeTimer);
    } else {
      startAuto();
    }
  });

  startAuto();
}
