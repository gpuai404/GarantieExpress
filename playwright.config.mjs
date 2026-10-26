import { defineConfig, devices } from "@playwright/test";
import { resolve } from "node:path";

const dataDirectory = resolve("test-results/e2e-data");

export default defineConfig({
    testDir: "./tests/e2e",
    fullyParallel: false,
    forbidOnly: Boolean(process.env.CI),
    retries: process.env.CI ? 2 : 0,
    workers: 1,
    reporter: process.env.CI ? "github" : "list",
    use: {
        baseURL: "http://127.0.0.1:5268",
        trace: "retain-on-failure",
        screenshot: "only-on-failure"
    },
    projects: [
        { name: "chromium", use: { browserName: "chromium" } },
        { name: "mobile-chromium", use: { ...devices["Pixel 7"] } }
    ],
    webServer: {
        command: "dotnet run --project src/GarantieExpress.Host/GarantieExpress.Host.csproj --configuration Release --no-launch-profile",
        env: {
            ASPNETCORE_ENVIRONMENT: "Development",
            GarantieExpress__Url: "http://127.0.0.1:5268",
            GarantieExpress__LaunchBrowser: "false",
            GarantieExpress__DataDirectory: dataDirectory,
            GarantieExpress__Updates__ApiUrl: ""
        },
        url: "http://127.0.0.1:5268",
        reuseExistingServer: false,
        timeout: 120_000
    }
});
