function nextFrame() {
    return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
}

const defaultWarrantySelector = ".warranty-print-target .warranty-document";
const paginationGuardMm = 1;
const pageViewportGuardMm = 0.2;

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

function isValidPageSize(widthMm, heightMm) {
    return Number.isFinite(widthMm) && widthMm > paginationGuardMm * 2
        && Number.isFinite(heightMm) && heightMm > paginationGuardMm * 2;
}

function createPrintRoot(documentElement, widthMm, heightMm) {
    const printRoot = document.createElement("div");
    printRoot.className = "garantie-express-print-root";
    printRoot.setAttribute("aria-hidden", "true");
    printRoot.style.width = `${widthMm - pageViewportGuardMm}mm`;
    printRoot.style.height = `${heightMm - pageViewportGuardMm}mm`;
    const scale = Math.min(
        (widthMm - paginationGuardMm * 2) / widthMm,
        (heightMm - paginationGuardMm * 2) / heightMm);
    printRoot.style.setProperty("--print-document-scale", scale.toString());
    printRoot.appendChild(documentElement.cloneNode(true));
    document.body.appendChild(printRoot);
    return printRoot;
}

export async function printWarranty(widthMm, heightMm, inkSaver, targetSelector = defaultWarrantySelector) {
    if (!isValidPageSize(widthMm, heightMm)) throw new Error("The warranty page size is invalid.");

    const documentElement = document.querySelector(targetSelector);
    if (!documentElement) throw new Error("The warranty document is unavailable for printing.");

    let pageStyle = document.getElementById("garantie-express-dynamic-page-style");
    if (!pageStyle) {
        pageStyle = document.createElement("style");
        pageStyle.id = "garantie-express-dynamic-page-style";
        document.head.appendChild(pageStyle);
    }

    let printRoot;

    try {
        pageStyle.textContent = `@page { size: ${widthMm}mm ${heightMm}mm; margin: 0; }`;
        document.body.classList.add("garantie-express-printing");
        document.body.classList.toggle("garantie-express-ink-saver", inkSaver);
        printRoot = createPrintRoot(documentElement, widthMm, heightMm);

        const printedDocument = printRoot.querySelector(".warranty-document");
        await waitForDocumentAssets(printedDocument);
        window.print();
    } finally {
        printRoot?.remove();
        document.body.classList.remove("garantie-express-printing", "garantie-express-ink-saver");
        pageStyle.textContent = "";
    }
}
