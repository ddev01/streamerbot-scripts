'use strict';

/**
 * Prettier plugin that formats C# via SbFormat (Roslyn NormalizeWhitespace),
 * matching Streamer.bot Execute C# Code → Format Document.
 */

const { spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const { hardline } = require('prettier').doc.builders;

const repoRoot = __dirname;
const dll = path.join(repoRoot, 'tools', 'SbFormat', 'bin', 'Release', 'net8.0', 'SbFormat.dll');
const project = path.join(repoRoot, 'tools', 'SbFormat', 'SbFormat.csproj');

function ensureBuilt() {
  if (fs.existsSync(dll)) return;
  const build = spawnSync(
    'dotnet',
    ['build', project, '-c', 'Release', '--nologo', '-v', 'q'],
    { encoding: 'utf8', cwd: repoRoot }
  );
  if (build.status !== 0) {
    throw new Error(
      `SbFormat build failed:\n${build.stdout || ''}\n${build.stderr || ''}`
    );
  }
}

function formatWithSb(text) {
  ensureBuilt();
  const result = spawnSync('dotnet', ['exec', dll, '--stdin'], {
    input: text,
    encoding: 'utf8',
    cwd: repoRoot,
    maxBuffer: 32 * 1024 * 1024,
  });
  if (result.status !== 0) {
    throw new Error(
      `SbFormat failed (exit ${result.status}):\n${result.stderr || result.stdout || ''}`
    );
  }
  // Normalize to LF; Prettier applies endOfLine (crlf) when serializing the doc.
  return result.stdout.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
}

function toDoc(formatted) {
  const lines = formatted.split('\n');
  // Drop a single trailing empty line from the final split so we don't add an
  // extra hardline beyond what SbFormat emitted.
  if (lines.length > 0 && lines[lines.length - 1] === '') {
    lines.pop();
  }
  const parts = [];
  for (let i = 0; i < lines.length; i++) {
    if (i > 0) parts.push(hardline);
    parts.push(lines[i]);
  }
  return parts;
}

const languages = [
  {
    name: 'C#',
    parsers: ['sb-csharp'],
    extensions: ['.cs'],
    vscodeLanguageIds: ['csharp'],
  },
];

const parsers = {
  'sb-csharp': {
    parse(text) {
      return { type: 'sb-csharp-source', text };
    },
    astFormat: 'sb-csharp-ast',
    locStart() {
      return 0;
    },
    locEnd(node) {
      return node.text ? node.text.length : 0;
    },
  },
};

const printers = {
  'sb-csharp-ast': {
    print(path) {
      return toDoc(formatWithSb(path.getValue().text));
    },
  },
};

module.exports = {
  languages,
  parsers,
  printers,
};
