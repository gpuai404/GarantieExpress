import { rm } from "node:fs/promises";
import { resolve } from "node:path";

const dataDirectory = resolve("test-results/e2e-data");
await rm(dataDirectory, { recursive: true, force: true });
