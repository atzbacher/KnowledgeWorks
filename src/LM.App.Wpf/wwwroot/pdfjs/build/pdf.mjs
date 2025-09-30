import * as pdfjsLib from "../src/pdf.js";

if (!globalThis.pdfjsLib) {
  globalThis.pdfjsLib = pdfjsLib;
}

export * from "../src/pdf.js";
export default pdfjsLib;
