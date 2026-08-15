export function attachClickHandler(svgElement, dotNetRef, margin, cell) {
    function handler(e) {
        const pt = svgElement.createSVGPoint();
        pt.x = e.clientX;
        pt.y = e.clientY;
        const loc = pt.matrixTransform(svgElement.getScreenCTM().inverse());
        const col = Math.floor((loc.x - margin) / cell);
        const row = Math.floor((loc.y - margin) / cell);
        dotNetRef.invokeMethodAsync('OnGridClick', col, row);
    }

    svgElement.addEventListener('click', handler);
}
