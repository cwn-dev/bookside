// "/" or Ctrl/⌘+K focuses the header search box; focus opens it (CSS).
// One document listener per page; attach() points it at the current box, so a
// new circuit re-rendering the header doesn't add a second one.
// Up/Down move between the box and the result links, in Tab order.
let input = null;

export function attach(element) {
    if (input === null) {
        document.addEventListener("keydown", focusSearch);
    }
    input = element;
    element.closest(".global-search").addEventListener("keydown", e => moveFocus(e, element));
}

function moveFocus(e, box) {
    const step = { ArrowDown: 1, ArrowUp: -1 }[e.key];
    if (!step || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) {
        return;
    }

    const stops = [box, ...box.closest(".global-search").querySelectorAll(".search-panel a[href]")];
    const current = stops.indexOf(document.activeElement);
    if (current < 0) {
        return;
    }

    // Also at either end, so the page behind the panel doesn't scroll.
    e.preventDefault();
    stops[current + step]?.focus();
}

function focusSearch(e) {
    if (!input?.isConnected || e.defaultPrevented || e.altKey) {
        return;
    }

    const ctrlK = (e.ctrlKey || e.metaKey) && !e.shiftKey && e.key.toLowerCase() === "k";
    const slash = e.key === "/" && !e.ctrlKey && !e.metaKey && !isTyping(e.target);
    if (ctrlK || slash) {
        e.preventDefault();
        input.focus();
        input.select();
    }
}

function isTyping(target) {
    return target instanceof HTMLElement
        && (target.isContentEditable || target.matches("input, textarea, select"));
}
