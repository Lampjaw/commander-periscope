// Two ways to enter "reposition" mode: hold the modifier key (desktop) or toggle it on with a button
// (touch devices have no keyboard, so the key-hold path alone would strand them).
export function attachOverlayDrag(overlayElement, modifierKey) {
    let dragging = false;
    let startX = 0, startY = 0, originX = 0, originY = 0;
    let offsetX = 0, offsetY = 0;
    let keyHeld = false;
    let toggledOn = false;

    function isActive() {
        return keyHeld || toggledOn;
    }

    function syncInteractivity() {
        const active = isActive();
        overlayElement.style.pointerEvents = active ? 'auto' : 'none';
        overlayElement.classList.toggle('radio-overlay-grid-active', active);
    }

    function keydown(e) {
        if (e.key === modifierKey) {
            keyHeld = true;
            syncInteractivity();
        }
    }

    function keyup(e) {
        if (e.key === modifierKey) {
            keyHeld = false;
            syncInteractivity();
        }
    }

    function pointerdown(e) {
        if (!isActive()) {
            return;
        }

        dragging = true;
        startX = e.clientX;
        startY = e.clientY;
        originX = offsetX;
        originY = offsetY;
        overlayElement.setPointerCapture(e.pointerId);
    }

    function pointermove(e) {
        if (!dragging) {
            return;
        }

        offsetX = originX + (e.clientX - startX);
        offsetY = originY + (e.clientY - startY);
        overlayElement.style.transform = `translate(${offsetX}px, ${offsetY}px)`;
    }

    function pointerup() {
        dragging = false;
    }

    window.addEventListener('keydown', keydown);
    window.addEventListener('keyup', keyup);
    overlayElement.addEventListener('pointerdown', pointerdown);
    overlayElement.addEventListener('pointermove', pointermove);
    overlayElement.addEventListener('pointerup', pointerup);
    overlayElement.addEventListener('pointercancel', pointerup);

    return {
        reset() {
            offsetX = 0;
            offsetY = 0;
            overlayElement.style.transform = 'translate(0px, 0px)';
        },
        setToggledOn(value) {
            toggledOn = value;
            syncInteractivity();
        },
    };
}
