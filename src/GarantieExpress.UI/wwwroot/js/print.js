function nextFrame() {
    return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
}

const defaultWarrantySelector = ".warranty-print-target .warranty-document";

async function waitForDocumentAssets(element) {
    await document.fonts?.ready;
    const images = [...element.querySelectorAll("img")];
    await Promise.all(images.map(image => image.complete
        ? Promise.resolve()
        : image.decode().catch(() => undefined)));
    await nextFrame();
}

export function elementFits(element, tolerance = 1) {
    return element.scrollWidth <= element.clientWidth + tolerance
        && element.scrollHeight <= element.clientHeight + tolerance;
}

export function childrenFit(element, tolerance = 1) {
    const bounds = element.getBoundingClientRect();
    return [...element.children].every(child => {
        const childBounds = child.getBoundingClientRect();
        return childBounds.left >= bounds.left - tolerance
            && childBounds.right <= bounds.right + tolerance
            && childBounds.top >= bounds.top - tolerance
            && childBounds.bottom <= bounds.bottom + tolerance;
    });
}

export async function warrantyFitsPage(targetSelector = defaultWarrantySelector) {
    document.body.classList.add("garantie-express-measuring-print");
    try {
        const documentElement = document.querySelector(targetSelector);
        if (!documentElement) return false;

        await waitForDocumentAssets(documentElement);

        const boundedElements = [...documentElement.querySelectorAll("[data-print-bounded]")];

        return childrenFit(documentElement)
            && boundedElements.every(element => elementFits(element));
    } finally {
        document.body.classList.remove("garantie-express-measuring-print");
    }
}

export async function printWarranty(widthMm, heightMm, inkSaver, targetSelector = defaultWarrantySelector) {
    let pageStyle = document.getElementById("garantie-express-dynamic-page-style");
    if (!pageStyle) {
        pageStyle = document.createElement("style");
        pageStyle.id = "garantie-express-dynamic-page-style";
        document.head.appendChild(pageStyle);
    }

    pageStyle.textContent = `@page { size: ${widthMm}mm ${heightMm}mm; margin: 0; }`;
    document.body.classList.add("garantie-express-printing");
    document.body.classList.toggle("garantie-express-ink-saver", inkSaver);

    try {
        const documentElement = document.querySelector(targetSelector);
        if (documentElement) await waitForDocumentAssets(documentElement);
        else await nextFrame();
        window.print();
    } finally {
        document.body.classList.remove("garantie-express-printing", "garantie-express-ink-saver");
        pageStyle.textContent = "";
    }
}
