function resolve(el) {
    return typeof el === 'string' ? document.querySelector(el) : el;
}

export function qs(selector, parent = document) {
    return resolve(parent).querySelector(selector);
}

export function qsa(selector, parent = document) {
    return resolve(parent).querySelectorAll(selector);
}

export function clear(el) {
    el.innerHTML = '';
}

export function placeholder(el, text) {
    el.innerHTML = `<div class="chat-placeholder">${text}</div>`;
}

export function div(cls, text = '') {
    const d = document.createElement('div');
    if (cls) d.className = cls;
    if (text) d.textContent = text;
    return d;
}

export function span(cls, text = '') {
    const s = document.createElement('span');
    if (cls) s.className = cls;
    if (text) s.textContent = text;
    return s;
}

export function isAtBottom(el) {
    return el.scrollHeight - el.scrollTop - el.clientHeight < 60;
}

export function scrollToBottom(el, instant = false) {
    if (instant) {
        el.scrollTop = el.scrollHeight;
    } else {
        el.scrollTo({top: el.scrollHeight, behavior: 'smooth'});
    }
}

export function setActive(container, selector, matchFn) {
    qsa(selector, container).forEach(el => {
        el.classList.toggle('active', matchFn(el));
    });
}

export function hashColor(str) {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
        hash = str.charCodeAt(i) + ((hash << 5) - hash);
    }
    const h = Math.abs(hash) % 360;
    return `hsl(${h}, 45%, 55%)`;
}
