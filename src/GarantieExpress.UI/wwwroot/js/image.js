function loadImage(source) {
    return new Promise((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve(image);
        image.onerror = () => reject(new Error("Invalid image data."));
        image.src = source;
    });
}

export async function downscaleImage(streamReference, contentType, maxWidth, maxHeight) {
    const bytes = await streamReference.arrayBuffer();
    const objectUrl = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    try {
        const image = await loadImage(objectUrl);
        const scale = Math.min(1, maxWidth / image.naturalWidth, maxHeight / image.naturalHeight);
        const width = Math.max(1, Math.round(image.naturalWidth * scale));
        const height = Math.max(1, Math.round(image.naturalHeight * scale));
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext("2d", { alpha: true });
        if (!context) throw new Error("Canvas is unavailable.");
        context.drawImage(image, 0, 0, width, height);
        const blob = await new Promise((resolve, reject) => canvas.toBlob(
            value => value ? resolve(value) : reject(new Error("Image processing failed.")),
            "image/png"));
        return DotNet.createJSStreamReference(blob);
    } finally {
        URL.revokeObjectURL(objectUrl);
    }
}
