window.opshub = {
    scrollToElement: function (id) {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        element.scrollIntoView({
            behavior: reducedMotion ? "instant" : "smooth",
            block: "start"
        });

        element.querySelector("input, textarea, select")?.focus({ preventScroll: true });
    }
};
