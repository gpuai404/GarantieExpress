import test from "node:test";
import assert from "node:assert/strict";
import { childrenFit, elementFits } from "../../src/GarantieExpress.UI/wwwroot/js/print.js";

test("elementFits accepts content within both dimensions", () => {
    assert.equal(elementFits({ scrollWidth: 100, clientWidth: 100, scrollHeight: 80, clientHeight: 80 }), true);
});

test("elementFits rejects clipped horizontal or vertical content", () => {
    assert.equal(elementFits({ scrollWidth: 102, clientWidth: 100, scrollHeight: 80, clientHeight: 80 }), false);
    assert.equal(elementFits({ scrollWidth: 100, clientWidth: 100, scrollHeight: 82, clientHeight: 80 }), false);
});

test("childrenFit detects content outside the printable root", () => {
    const child = { getBoundingClientRect: () => ({ left: 0, right: 101, top: 0, bottom: 80 }) };
    const root = {
        children: [child],
        getBoundingClientRect: () => ({ left: 0, right: 100, top: 0, bottom: 100 })
    };

    assert.equal(childrenFit(root), true);
    child.getBoundingClientRect = () => ({ left: 0, right: 103, top: 0, bottom: 80 });
    assert.equal(childrenFit(root), false);
});
