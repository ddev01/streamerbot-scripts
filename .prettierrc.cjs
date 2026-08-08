/** @type {import('prettier').Config} */
module.exports = {
  plugins: ['./prettier-plugin-sb-csharp.cjs'],
  // Non-C# defaults (C# is fully owned by SbFormat / Streamer.bot style).
  endOfLine: 'crlf',
  tabWidth: 4,
  useTabs: false,
  printWidth: 100,
  singleQuote: true,
  trailingComma: 'es5',
  overrides: [
    {
      files: '*.cs',
      options: {
        // Parser comes from prettier-plugin-sb-csharp; keep editor-aligned indent metadata.
        tabWidth: 4,
        useTabs: false,
        endOfLine: 'crlf',
      },
    },
  ],
};
