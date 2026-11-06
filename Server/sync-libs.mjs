// Syncs third-party browser libraries from node_modules into Server/wwwroot/lib,
// mirroring the layout the viewer pages reference (formerly done by LibMan).
// Usage: npm install && npm run sync-libs
import { cpSync, existsSync, mkdirSync, readdirSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const serverDir = resolve(dirname(fileURLToPath(import.meta.url)));
const modulesDir = join(serverDir, "node_modules");
const libDir = join(serverDir, "wwwroot", "lib");

function copyFiles(srcDir, destDir, filterFn) {
  if (!existsSync(srcDir)) {
    throw new Error(`Source directory missing: ${srcDir}`);
  }
  mkdirSync(destDir, { recursive: true });
  let copied = 0;
  for (const entry of readdirSync(srcDir)) {
    const srcPath = join(srcDir, entry);
    if (statSync(srcPath).isDirectory()) continue;
    if (filterFn && !filterFn(entry)) continue;
    cpSync(srcPath, join(destDir, entry));
    copied++;
  }
  return copied;
}

console.log("Syncing browser libraries into wwwroot/lib ...");

// 1. @microsoft/signalr -> lib/microsoft-signalr/ (cdnjs layout: dist/browser/*)
const signalrCopied = copyFiles(
  join(modulesDir, "@microsoft", "signalr", "dist", "browser"),
  join(libDir, "microsoft-signalr")
);
console.log(`  @microsoft/signalr: ${signalrCopied} file(s)`);

// 2. @microsoft/signalr-protocol-msgpack -> lib/microsoft/signalr-protocol-msgpack/dist/browser/
const msgpackProtocolCopied = copyFiles(
  join(modulesDir, "@microsoft", "signalr-protocol-msgpack", "dist", "browser"),
  join(libDir, "microsoft", "signalr-protocol-msgpack", "dist", "browser")
);
console.log(`  @microsoft/signalr-protocol-msgpack: ${msgpackProtocolCopied} file(s)`);

// 3. @msgpack/msgpack -> lib/msgpack/dist.es5+umd/
const msgpackCopied = copyFiles(
  join(modulesDir, "@msgpack", "msgpack", "dist.es5+umd"),
  join(libDir, "msgpack", "dist.es5+umd"),
  (name) => name.startsWith("msgpack.")
);
console.log(`  @msgpack/msgpack: ${msgpackCopied} file(s)`);

// 4. @fortawesome/fontawesome-free -> lib/fontawesome/ (css, js, webfonts only)
let fontawesomeCopied = 0;
for (const sub of ["css", "js", "webfonts"]) {
  fontawesomeCopied += copyFiles(
    join(modulesDir, "@fortawesome", "fontawesome-free", sub),
    join(libDir, "fontawesome", sub)
  );
}
console.log(`  @fortawesome/fontawesome-free: ${fontawesomeCopied} file(s)`);

console.log("Done. Review `git diff --stat Server/wwwroot/lib` before committing.");
