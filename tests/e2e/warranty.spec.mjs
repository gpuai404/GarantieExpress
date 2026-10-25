import { test, expect } from "@playwright/test";

test.beforeEach(async ({ page }) => {
    await page.goto("/uitgeven");
    await expect(page.getByTestId("warranty-preview")).toBeVisible();
});

test("issue form updates every certificate without clipping", async ({ page }) => {
    await page.locator("#issue-brand-model").fill("Enterprise Test Phone 512 GB");
    await page.locator("#issue-identifier").fill("IMEI-123456789012345");
    await page.locator("#issue-price").fill("1299.95");

    await expect(page.locator("#issue-template option")).toHaveCount(2);
    await expect(page.locator("#issue-template option")).toHaveText(["Original", "Classic Navy"]);
    await expect(page.locator("#issue-page-size option")).toHaveCount(3);
    await expect(page.locator("#issue-page-size option")).toHaveText([
        "A4 · 210 × 297 mm",
        "A5 · 148 × 210 mm",
        "A6 · 105 × 148 mm"
    ]);

    const choices = [
        ["original", "a4", "a4-reference"],
        ["original", "a5", "a5-compact"],
        ["original", "a6", "a6-essential"],
        ["classic-navy", "a4", "a4-classic-navy"],
        ["classic-navy", "a5", "a5-classic-navy"],
        ["classic-navy", "a6", "a6-classic-navy"]
    ];

    for (const [design, pageSize, template] of choices) {
        await page.locator("#issue-template").selectOption(design);
        await page.locator("#issue-page-size").selectOption(pageSize);
        const document = page.locator(".warranty-print-target .warranty-document");
        await expect(document).toHaveAttribute("data-template", template);
        await expect(document.locator(".coverage-dates p")).toHaveCount(2);

        const fits = await page.evaluate(async () => {
            const print = await import("/_content/GarantieExpress.UI/js/print.js");
            return print.warrantyFitsPage();
        });
        expect(fits).toBe(true);
    }
});

test("navigation exposes keyboard and expanded state", async ({ page }, testInfo) => {
    await page.goto("/");
    const skipLink = page.locator(".skip-link");
    await skipLink.focus();
    await expect(skipLink).toBeFocused();
    await expect(skipLink).toBeVisible();

    const toggle = page.locator(".navigation-toggle");
    await expect(toggle).toHaveAttribute("aria-expanded", "false");
    if (testInfo.project.name === "mobile-chromium") {
        await toggle.click();
        await expect(toggle).toHaveAttribute("aria-expanded", "true");
        await expect(page.locator("#primary-navigation")).toBeVisible();
    } else {
        await expect(toggle).toBeHidden();
        await expect(page.locator("#primary-navigation")).toBeVisible();
    }
});

test("category actions render their result immediately", async ({ page }) => {
    await page.goto("/categorieen");
    const duplicate = page.getByTestId("category-duplicate").first();
    await expect(duplicate).toBeVisible();

    await duplicate.click();

    await expect(page.locator(".category-editor")).toBeVisible();
    await page.locator(".editor-actions .button.secondary").click();
    await expect(page.locator(".category-editor")).toBeHidden();

    const activeCard = page.locator(".category-card:not(.archived)").first();
    const categoryName = await activeCard.locator("h2").innerText();
    await activeCard.getByTestId("category-archive").click();

    const archivedCard = page.locator(".category-card").filter({
        has: page.getByRole("heading", { name: categoryName, exact: true })
    });
    await expect(archivedCard).toHaveClass(/archived/);
});

test("history actions render immediately and keep void visible when disabled", async ({ page }) => {
    await page.locator("#issue-brand-model").fill("History action test");
    await page.locator("#issue-identifier").fill("HISTORY-001");
    await page.locator("#issue-price").fill("149.95");
    await page.locator('button[type="submit"]').click();
    await expect(page.locator(".preview-number")).toContainText(/-\d{4}-\d{4,}/);

    await page.goto("/historiek");
    await expect(page.locator(".history-row").first()).toHaveClass(/active/);
    await page.getByTestId("history-open").first().click();
    await expect(page.locator(".history-layout .document-preview-panel")).toBeVisible();

    await page.getByTestId("history-delete").first().click();
    await expect(page.locator(".history-confirm")).toBeVisible();
    await page.locator(".history-confirm .button.secondary").click();
    await expect(page.locator(".history-confirm")).toBeHidden();

    const voidButton = page.getByTestId("history-void").first();
    await expect(voidButton).toBeEnabled();
    await voidButton.click();
    await expect(page.locator(".history-confirm")).toBeVisible();

    await page.locator(".history-confirm .button.danger").click();

    await expect(page.locator(".history-row").first()).toHaveClass(/voided/);
    await expect(page.getByTestId("history-void").first()).toBeVisible();
    await expect(page.getByTestId("history-void").first()).toBeDisabled();
});

test("history derives expired state from the valid-until date", async ({ page }, testInfo) => {
    const serial = `EXPIRED-${testInfo.project.name}`;
    await page.locator("#issue-brand-model").fill("Expired warranty test");
    await page.locator("#issue-identifier").fill(serial);
    await page.locator("#issue-valid-from").fill("2020-01-01");
    await page.locator("#issue-price").fill("99");
    await page.locator('button[type="submit"]').click();
    await expect(page.locator(".preview-number")).toContainText(/-\d{4}-\d{4,}/);

    await page.goto("/historiek");
    await page.locator(".history-search input").fill(serial);
    const row = page.locator(".history-row");
    await expect(row).toHaveCount(1);
    await expect(row).toHaveClass(/expired/);
    await expect(row.locator(".history-number em")).toHaveText("Expired");
});
