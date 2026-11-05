// Keys the tag picker handles while it is open, so the textarea doesn't move
// the caret or insert a newline. Blazor still receives the keydown.
const pickerKeys = {
    open: ["ArrowUp", "ArrowDown", "Escape"],
    active: ["ArrowUp", "ArrowDown", "Escape", "Enter", "Tab"],
};

export function attach(textarea) {
    textarea.addEventListener("keydown", e => {
        const keys = pickerKeys[textarea.dataset.picker];
        if (keys && !e.ctrlKey && !e.metaKey && !e.altKey && keys.includes(e.key)) {
            e.preventDefault();
        }
    });
}

// Styles that decide where text wraps, copied to the hidden mirror below.
const mirrored = [
    "boxSizing", "width", "borderTopWidth", "borderRightWidth", "borderBottomWidth",
    "borderLeftWidth", "paddingTop", "paddingRight", "paddingBottom", "paddingLeft",
    "fontFamily", "fontSize", "fontStyle", "fontWeight", "fontStretch", "lineHeight",
    "letterSpacing", "wordSpacing", "textIndent", "textTransform", "tabSize",
];

// The caret's index, and the point just below its line relative to the
// textarea's offset parent (the picker's containing block). A hidden div with
// the same box and font, holding the text up to the caret, shows where it is.
export function caret(textarea) {
    const position = textarea.selectionStart;
    const style = getComputedStyle(textarea);
    const mirror = document.createElement("div");
    for (const name of mirrored) {
        mirror.style[name] = style[name];
    }
    Object.assign(mirror.style, {
        position: "absolute",
        visibility: "hidden",
        top: "0",
        left: "-9999px",
        whiteSpace: "pre-wrap",
        overflowWrap: "break-word",
    });
    mirror.textContent = textarea.value.slice(0, position);
    const marker = document.createElement("span");
    marker.textContent = "\u200b";
    mirror.appendChild(marker);
    document.body.appendChild(mirror);

    const lineHeight = marker.offsetHeight;
    const top = Math.min(
        textarea.offsetTop + marker.offsetTop - textarea.scrollTop + lineHeight,
        textarea.offsetTop + textarea.offsetHeight);
    const left = textarea.offsetLeft + marker.offsetLeft - textarea.scrollLeft;
    mirror.remove();

    return { position, top, left };
}

export function setCaret(textarea, position) {
    textarea.focus();
    textarea.setSelectionRange(position, position);
}
