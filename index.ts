import { cpSync, existsSync, mkdirSync, readdirSync } from 'fs';
import { join, resolve } from 'path';

const releaseDir = resolve('./nupkg');
const destDir = resolve('../backend/local-packages');

// Find the latest .nupkg by modified time
const nupkgs = readdirSync(releaseDir)
  .filter((f) => f.endsWith('.nupkg'))
  .map((f) => {
    const full = join(releaseDir, f);
    return { name: f, path: full, mtime: Bun.file(full).lastModified };
  })
  .sort((a, b) => b.mtime - a.mtime);

if (nupkgs.length === 0) {
  console.error('No .nupkg files found in', releaseDir);
  process.exit(1);
}

const latest = nupkgs[0]!;

if (!existsSync(destDir)) {
  mkdirSync(destDir, { recursive: true });
}

const dest = join(destDir, latest.name);
cpSync(latest.path, dest);
console.log(`Copied ${latest.name} -> ${dest}`);
